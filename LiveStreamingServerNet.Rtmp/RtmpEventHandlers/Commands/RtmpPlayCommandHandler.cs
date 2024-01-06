﻿using LiveStreamingServerNet.Rtmp.Contracts;
using LiveStreamingServerNet.Rtmp.Logging;
using LiveStreamingServerNet.Rtmp.RtmpEventHandlers.CommandDispatcher;
using LiveStreamingServerNet.Rtmp.RtmpEventHandlers.CommandDispatcher.Attributes;
using LiveStreamingServerNet.Rtmp.Services;
using LiveStreamingServerNet.Rtmp.Services.Contracts;
using LiveStreamingServerNet.Rtmp.Services.Extensions;
using LiveStreamingServerNet.Rtmp.Utilities;
using Microsoft.Extensions.Logging;

namespace LiveStreamingServerNet.Rtmp.RtmpEventHandlers.Commands
{
    internal record RtmpPlayCommand(double TransactionId, IDictionary<string, object> CommandObject, string StreamName, double Start, double Duration, bool Reset);

    [RtmpCommand("play")]
    internal class RtmpPlayCommandHandler : RtmpCommandHandler<RtmpPlayCommand>
    {
        private readonly IRtmpStreamManagerService _streamManager;
        private readonly IRtmpCommandMessageSenderService _commandMessageSender;
        private readonly IRtmpMediaMessageManagerService _mediaMessageManager;
        private readonly ILogger<RtmpPlayCommandHandler> _logger;

        public RtmpPlayCommandHandler(
            IRtmpStreamManagerService streamManager,
            IRtmpCommandMessageSenderService commandMessageSender,
            IRtmpMediaMessageManagerService mediaMessageManager,
            ILogger<RtmpPlayCommandHandler> logger)
        {
            _streamManager = streamManager;
            _commandMessageSender = commandMessageSender;
            _mediaMessageManager = mediaMessageManager;
            _logger = logger;
        }

        public override async Task<bool> HandleAsync(
            IRtmpChunkStreamContext chunkStreamContext,
            IRtmpClientPeerContext peerContext,
            RtmpPlayCommand command,
            CancellationToken cancellationToken)
        {
            _logger.Play(peerContext.Peer.PeerId, command.StreamName);

            if (peerContext.StreamId == null)
                throw new InvalidOperationException("Stream is not yet created.");

            var (streamPath, streamArguments) = ParseSubscriptionContext(command, peerContext);

            if (await AuthorizeAsync(peerContext, command, chunkStreamContext, streamPath, streamArguments))
            {
                StartSubscribing(peerContext, command, chunkStreamContext, streamPath, streamArguments);
            }

            return true;
        }

        private Task<bool> AuthorizeAsync(IRtmpClientPeerContext peerContext, string streamPath, IDictionary<string, string> streamArguments)
        {
            return Task.FromResult(true);
        }

        private static (string StreamPath, IDictionary<string, string> StreamArguments)
            ParseSubscriptionContext(RtmpPlayCommand command, IRtmpClientPeerContext peerContext)
        {
            var (streamName, arguments) = StreamUtilities.ParseStreamPath(command.StreamName);

            var streamPath = $"/{string.Join('/',
                new string[] { peerContext.AppName, streamName }.Where(s => !string.IsNullOrEmpty(s)).ToArray())}";

            return (streamPath, arguments);
        }

        private async Task<bool> AuthorizeAsync(
            IRtmpClientPeerContext peerContext,
            RtmpPlayCommand command,
            IRtmpChunkStreamContext chunkStreamContext,
            string streamPath,
            IDictionary<string, string> streamArguments)
        {
            if (!await AuthorizeAsync(peerContext, streamPath, streamArguments))
            {
                _logger.AuthorizationFailed(peerContext.Peer.PeerId, streamPath);
                SendAuthorizationFailedCommandMessage(peerContext, chunkStreamContext);
                return false;
            }

            return true;
        }

        private bool StartSubscribing(
            IRtmpClientPeerContext peerContext,
            RtmpPlayCommand command,
            IRtmpChunkStreamContext chunkStreamContext,
            string streamPath,
            IDictionary<string, string> streamArguments)
        {
            var startSubscribingResult = _streamManager.StartSubscribingStream(peerContext, chunkStreamContext.ChunkStreamId, streamPath, streamArguments);

            switch (startSubscribingResult)
            {
                case SubscribingStreamResult.Succeeded:
                    _logger.SubscriptionStarted(peerContext.Peer.PeerId, streamPath);
                    SendSubscriptionStartedMessage(peerContext, chunkStreamContext);
                    SendCachedStreamMessages(peerContext, chunkStreamContext);
                    return true;

                case SubscribingStreamResult.AlreadySubscribing:
                    _logger.AlreadySubscribing(peerContext.Peer.PeerId, streamPath);
                    SendBadConnectionCommandMessage(peerContext, chunkStreamContext, "Already subscribing.");
                    return false;

                case SubscribingStreamResult.AlreadyPublishing:
                    _logger.AlreadyPublishing(peerContext.Peer.PeerId, streamPath);
                    SendBadConnectionCommandMessage(peerContext, chunkStreamContext, "Already publishing.");
                    return false;

                default:
                    throw new ArgumentOutOfRangeException(nameof(startSubscribingResult), startSubscribingResult, null);
            }
        }

        private void SendCachedStreamMessages(IRtmpClientPeerContext peerContext, IRtmpChunkStreamContext chunkStreamContext)
        {
            var publishStreamContext = _streamManager.GetPublishStreamContext(peerContext.StreamSubscriptionContext!.StreamPath);

            if (publishStreamContext == null)
                return;

            _mediaMessageManager.SendCachedStreamMetaDataMessage(
                peerContext, publishStreamContext,
                chunkStreamContext.MessageHeader.Timestamp,
                chunkStreamContext.MessageHeader.MessageStreamId);

            _mediaMessageManager.SendCachedHeaderMessages(
                peerContext, publishStreamContext,
                chunkStreamContext.MessageHeader.Timestamp,
                chunkStreamContext.MessageHeader.MessageStreamId);
        }

        private void SendAuthorizationFailedCommandMessage(IRtmpClientPeerContext peerContext, IRtmpChunkStreamContext chunkStreamContext)
        {
            _commandMessageSender.SendOnStatusCommandMessageAsync(
                peerContext,
                chunkStreamContext.ChunkStreamId,
                RtmpArgumentValues.Error,
                RtmpStatusCodes.PublishUnauthorized,
                "Authorization failed.");
        }

        private void SendSubscriptionStartedMessage(IRtmpClientPeerContext peerContext, IRtmpChunkStreamContext chunkStreamContext)
        {
            _commandMessageSender.SendOnStatusCommandMessageAsync(
                peerContext,
                chunkStreamContext.ChunkStreamId,
                RtmpArgumentValues.Status,
                RtmpStatusCodes.PlayStart,
                "Stream subscribed.");
        }

        private void SendBadConnectionCommandMessage(IRtmpClientPeerContext peerContext, IRtmpChunkStreamContext chunkStreamContext, string reason)
        {
            _commandMessageSender.SendOnStatusCommandMessageAsync(
                peerContext,
                chunkStreamContext.ChunkStreamId,
                RtmpArgumentValues.Error,
                RtmpStatusCodes.PlayBadConnection,
                reason);
        }
    }
}