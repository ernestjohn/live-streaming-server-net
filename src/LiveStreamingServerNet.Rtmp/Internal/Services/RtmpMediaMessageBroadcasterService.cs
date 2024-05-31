﻿using LiveStreamingServerNet.Networking.Contracts;
using LiveStreamingServerNet.Networking.Exceptions;
using LiveStreamingServerNet.Rtmp.Internal.Contracts;
using LiveStreamingServerNet.Rtmp.Internal.Logging;
using LiveStreamingServerNet.Rtmp.Internal.MediaPackageDiscarding.Contracts;
using LiveStreamingServerNet.Rtmp.Internal.RtmpEventHandlers;
using LiveStreamingServerNet.Rtmp.Internal.RtmpHeaders;
using LiveStreamingServerNet.Rtmp.Internal.Services.Contracts;
using LiveStreamingServerNet.Utilities;
using LiveStreamingServerNet.Utilities.Contracts;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace LiveStreamingServerNet.Rtmp.Internal.Services
{
    internal class RtmpMediaMessageBroadcasterService : IRtmpMediaMessageBroadcasterService
    {
        private readonly IRtmpChunkMessageWriterService _chunkMessageWriter;
        private readonly IRtmpMediaMessageInterceptionService _interception;
        private readonly INetBufferPool _netBufferPool;
        private readonly IMediaPackageDiscarderFactory _mediaPackageDiscarderFactory;
        private readonly ILogger _logger;
        private readonly IBufferPool? _bufferPool;
        private readonly ConcurrentDictionary<IRtmpClientContext, ClientMediaContext> _clientMediaContexts = new();
        private readonly ConcurrentDictionary<IRtmpClientContext, Task> _clientTasks = new();

        public RtmpMediaMessageBroadcasterService(
            IRtmpChunkMessageWriterService chunkMessageWriter,
            IRtmpMediaMessageInterceptionService interception,
            INetBufferPool netBufferPool,
            IMediaPackageDiscarderFactory mediaPackageDiscarderFactory,
            ILogger<RtmpMediaMessageBroadcasterService> logger,
            IBufferPool? bufferPool = null)
        {
            _chunkMessageWriter = chunkMessageWriter;
            _interception = interception;
            _netBufferPool = netBufferPool;
            _mediaPackageDiscarderFactory = mediaPackageDiscarderFactory;
            _logger = logger;
            _bufferPool = bufferPool;
        }

        private ClientMediaContext? GetMediaContext(IRtmpClientContext clientContext)
        {
            return _clientMediaContexts.GetValueOrDefault(clientContext);
        }

        public void RegisterClient(IRtmpClientContext clientContext)
        {
            _clientMediaContexts[clientContext] = new ClientMediaContext(clientContext, _mediaPackageDiscarderFactory);

            var clientTask = Task.Run(() => ClientTask(clientContext));
            _clientTasks[clientContext] = clientTask;
            _ = clientTask.ContinueWith(_ => _clientTasks.TryRemove(clientContext, out var _));
        }

        public void UnregisterClient(IRtmpClientContext clientContext)
        {
            if (_clientMediaContexts.TryRemove(clientContext, out var context))
            {
                context.Stop();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Task.WhenAll(_clientTasks.Values);
        }

        public async ValueTask BroadcastMediaMessageAsync(
            IRtmpPublishStreamContext publishStreamContext,
            IReadOnlyList<IRtmpClientContext> subscribers,
            MediaType mediaType,
            uint timestamp,
            bool isSkippable,
            INetBuffer payloadBuffer)
        {
            await _interception.ReceiveMediaMessageAsync(publishStreamContext.StreamPath, mediaType, payloadBuffer, timestamp, isSkippable);

            subscribers = subscribers.Where((subscriber) => FilterSubscribers(subscriber, isSkippable)).ToList();

            if (subscribers.Any())
                EnqueueMediaPackages(subscribers, mediaType, payloadBuffer, timestamp, publishStreamContext.StreamId, isSkippable);

            bool FilterSubscribers(IRtmpClientContext subscriber, bool isSkippable)
            {
                var subscriptionContext = subscriber.StreamSubscriptionContext;

                if (subscriptionContext == null)
                    return false;

                if (!isSkippable)
                    return true;

                switch (mediaType)
                {
                    case MediaType.Audio:
                        if (!subscriptionContext.IsReceivingAudio)
                            return false;
                        break;
                    case MediaType.Video:
                        if (!subscriptionContext.IsReceivingVideo)
                            return false;
                        break;
                }

                return true;
            }
        }

        private void EnqueueMediaPackages(
            IReadOnlyList<IRtmpClientContext> subscribersList,
            MediaType type,
            INetBuffer payloadBuffer,
            uint timestamp,
            uint streamId,
            bool isSkippable)
        {
            var basicHeader = new RtmpChunkBasicHeader(
                    0,
                    type == MediaType.Video ?
                    RtmpConstants.VideoMessageChunkStreamId :
                    RtmpConstants.AudioMessageChunkStreamId);

            var messageHeader = new RtmpChunkMessageHeaderType0(
                timestamp,
                payloadBuffer.Size,
                type == MediaType.Video ?
                RtmpMessageType.VideoMessage :
                RtmpMessageType.AudioMessage,
                streamId);

            foreach (var subscribersGroup in subscribersList.GroupBy(x => x.OutChunkSize))
            {
                var outChunkSize = subscribersGroup.Key;
                var subscribers = subscribersGroup.ToList();

                using var tempBuffer = _netBufferPool.Obtain();
                _chunkMessageWriter.Write(tempBuffer, basicHeader, messageHeader, payloadBuffer.MoveTo(0), outChunkSize);

                var rentedBuffer = new RentedBuffer(_bufferPool, tempBuffer.Size, subscribers.Count);
                tempBuffer.MoveTo(0).ReadBytes(rentedBuffer.Buffer, 0, rentedBuffer.Size);

                var mediaPackage = new ClientMediaPackage(rentedBuffer, isSkippable, timestamp, type);

                foreach (var subscriber in subscribers)
                {
                    var mediaContext = GetMediaContext(subscriber);
                    if (mediaContext == null || !mediaContext.AddPackage(ref mediaPackage))
                        rentedBuffer.Unclaim();
                }
            }
        }

        private async Task ClientTask(IRtmpClientContext clientContext)
        {
            var context = _clientMediaContexts.GetValueOrDefault(clientContext);

            if (context == null)
                return;

            var cancellation = context.CancellationToken;
            var subscriptionInitialized = false;

            try
            {
                while (!cancellation.IsCancellationRequested)
                {
                    var package = await context.ReadPackageAsync(cancellation);

                    try
                    {
                        if (clientContext.StreamSubscriptionContext == null)
                            continue;

                        if (!clientContext.UpdateTimestamp(package.Timestamp, package.MediaType) && package.IsSkippable)
                            continue;

                        if (!subscriptionInitialized)
                        {
                            await clientContext.StreamSubscriptionContext.UntilInitializationComplete();
                            subscriptionInitialized = true;
                        }

                        await clientContext.Client.SendAsync(package.RentedPayload);
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                    catch (BufferSendingException) when (!context.ClientContext.Client.IsConnected) { }
                    catch (Exception ex)
                    {
                        _logger.FailedToSendMediaMessage(clientContext.Client.ClientId, ex);
                    }
                    finally
                    {
                        package.RentedPayload.Unclaim();
                    }
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (ChannelClosedException) { }

            while (context.ReadPackage(out var package))
            {
                package.RentedPayload.Unclaim();
            }
        }

        private class ClientMediaContext
        {
            public readonly IRtmpClientContext ClientContext;
            public readonly CancellationToken CancellationToken;
            public long OutstandingPackagesSize => _outstandingPackagesSize;
            public long OutstandingPackagesCount => _outstandingPackageCount;

            private readonly IMediaPackageDiscarder _mediaPackageDiscarder;
            private readonly Channel<ClientMediaPackage> _packageChannel;
            private readonly CancellationTokenSource _cts;

            private long _outstandingPackagesSize;
            private long _outstandingPackageCount;

            public ClientMediaContext(IRtmpClientContext clientContext, IMediaPackageDiscarderFactory mediaPackageDiscarderFactory)
            {
                ClientContext = clientContext;
                _mediaPackageDiscarder = mediaPackageDiscarderFactory.Create(clientContext.Client.ClientId);

                _packageChannel = Channel.CreateUnbounded<ClientMediaPackage>(
                    new UnboundedChannelOptions { SingleReader = true, AllowSynchronousContinuations = true });
                _cts = new CancellationTokenSource();
                CancellationToken = _cts.Token;
            }

            public void Stop()
            {
                _packageChannel.Writer.Complete();
                _cts.Cancel();
            }

            public bool AddPackage(ref ClientMediaPackage package)
            {
                if (ShouldSkipPackage(this, package.IsSkippable))
                {
                    return false;
                }

                if (!_packageChannel.Writer.TryWrite(package))
                {
                    return false;
                }

                Interlocked.Add(ref _outstandingPackagesSize, package.RentedPayload.Size);
                Interlocked.Increment(ref _outstandingPackageCount);
                return true;
            }

            public async ValueTask<ClientMediaPackage> ReadPackageAsync(CancellationToken cancellation)
            {
                var package = await _packageChannel.Reader.ReadAsync(cancellation);
                Interlocked.Add(ref _outstandingPackagesSize, -package.RentedPayload.Size);
                Interlocked.Decrement(ref _outstandingPackageCount);
                return package;
            }

            public bool ReadPackage(out ClientMediaPackage package)
            {
                return _packageChannel.Reader.TryRead(out package);
            }

            private bool ShouldSkipPackage(ClientMediaContext context, bool isSkippable)
            {
                return _mediaPackageDiscarder.ShouldDiscardMediaPackage(
                    isSkippable, context.OutstandingPackagesSize, context.OutstandingPackagesCount);
            }
        }

        private record struct ClientMediaPackage(IRentedBuffer RentedPayload, bool IsSkippable, uint Timestamp, MediaType MediaType);
    }
}
