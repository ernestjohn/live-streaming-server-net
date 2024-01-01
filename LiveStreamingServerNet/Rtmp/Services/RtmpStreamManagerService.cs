﻿using LiveStreamingServerNet.Rtmp.Contracts;
using LiveStreamingServerNet.Rtmp.Services.Contracts;
using LiveStreamingServerNet.Utilities.Contracts;
using LiveStreamingServerNet.Utilities;
using Open.Threading;

namespace LiveStreamingServerNet.Rtmp.Services
{
    public class RtmpStreamManagerService : IRtmpStreamManagerService
    {
        private readonly ReaderWriterLockSlim _publishingRwLock = new();
        private readonly Dictionary<IRtmpClientPeerContext, string> _publishStreamPaths = new();
        private readonly Dictionary<string, IRtmpClientPeerContext> _publishingClientPeerContexts = new();

        private readonly ReaderWriterLockSlim _subscribingRwLock = new();
        private readonly Dictionary<string, List<IRtmpClientPeerContext>> _subscribingClientPeerContexts = new();
        private readonly Dictionary<IRtmpClientPeerContext, string> _subscribedStreamPaths = new();

        public string? GetPublishStreamPath(IRtmpClientPeerContext publisherPeerContext)
        {
            return _publishStreamPaths.GetValueOrDefault(publisherPeerContext);
        }

        public IRtmpClientPeerContext? GetPublishingClientPeerContext(string publishStreamPath)
        {
            using var readLock = _publishingRwLock.ReadLock();
            return _publishingClientPeerContexts.GetValueOrDefault(publishStreamPath);
        }

        public PublishingStreamResult StartPublishingStream(IRtmpClientPeerContext publisherPeerContext, string streamPath, IDictionary<string, string> streamArguments, out IList<IRtmpClientPeerContext>? existingSubscribers)
        {
            using var readLock = _publishingRwLock.WriteLock();
            using var subscribingWriteLock = _subscribingRwLock.WriteLock();

            existingSubscribers = null;

            if (_publishStreamPaths.ContainsKey(publisherPeerContext))
                return PublishingStreamResult.AlreadyPublishing;

            if (_publishingClientPeerContexts.ContainsKey(streamPath))
                return PublishingStreamResult.AlreadyExists;

            publisherPeerContext.PublishStreamContext!.StreamPath = streamPath;
            publisherPeerContext.PublishStreamContext!.StreamArguments = streamArguments;

            _publishStreamPaths.Add(publisherPeerContext, streamPath);
            _publishingClientPeerContexts.Add(streamPath, publisherPeerContext);

            existingSubscribers = _subscribingClientPeerContexts.GetValueOrDefault(streamPath)?.ToList();

            return PublishingStreamResult.Succeeded;
        }

        public bool StopPublishingStream(IRtmpClientPeerContext publisherPeerContext, out IList<IRtmpClientPeerContext>? existingSubscribers)
        {
            using var publishingWriteLock = _publishingRwLock.WriteLock();
            using var subscribingWriteLock = _subscribingRwLock.WriteLock();

            existingSubscribers = null;

            if (!_publishStreamPaths.TryGetValue(publisherPeerContext, out var publishStreamPath))
                return false;

            _publishingClientPeerContexts.Remove(publishStreamPath);
            _publishStreamPaths.Remove(publisherPeerContext);

            existingSubscribers = _subscribingClientPeerContexts.GetValueOrDefault(publishStreamPath)?.ToList();

            return true;
        }

        public bool IsStreamPathPublishing(string publishStreamPath)
        {
            using var readLock = _publishingRwLock.ReadLock();
            return _publishingClientPeerContexts.ContainsKey(publishStreamPath);
        }

        public SubscribingStreamResult StartSubscribingStream(IRtmpClientPeerContext subscriberPeerContext, uint chunkStreamId, string streamPath, IDictionary<string, string> streamArguments)
        {
            using var subscribingWriteLock = _subscribingRwLock.WriteLock();

            if (!_subscribingClientPeerContexts.TryGetValue(streamPath, out var subscribers))
            {
                subscribers = new List<IRtmpClientPeerContext>();
                _subscribingClientPeerContexts[streamPath] = subscribers;
            }

            if (_subscribedStreamPaths.ContainsKey(subscriberPeerContext))
                return SubscribingStreamResult.AlreadySubscribing;

            subscriberPeerContext.CreateStreamSubscriptionContext(chunkStreamId, streamPath, streamArguments);

            subscribers.Add(subscriberPeerContext);
            _subscribedStreamPaths.Add(subscriberPeerContext, streamPath);

            return SubscribingStreamResult.Succeeded;
        }

        public bool StopSubscribingStream(IRtmpClientPeerContext subscriberPeerContext)
        {
            using var subscribingWriteLock = _subscribingRwLock.WriteLock();

            if (!_subscribedStreamPaths.Remove(subscriberPeerContext, out var publishStreamPath))
                return false;

            if (_subscribingClientPeerContexts.TryGetValue(publishStreamPath, out var subscribers))
            {
                subscribers.Remove(subscriberPeerContext);
            }

            return true;
        }

        public IRentable<IList<IRtmpClientPeerContext>> GetSubscribersLocked(string publishStreamPath)
        {
            var readLock = _subscribingRwLock.ReadLock();

            return new Rentable<IList<IRtmpClientPeerContext>>(
                _subscribingClientPeerContexts.GetValueOrDefault(publishStreamPath)?.ToList() ?? new List<IRtmpClientPeerContext>(),
                readLock.Dispose);
        }

        public IList<IRtmpClientPeerContext> GetSubscribers(string publishStreamPath)
        {
            using var readLock = _subscribingRwLock.ReadLock();
            return _subscribingClientPeerContexts.GetValueOrDefault(publishStreamPath)?.ToList() ?? new List<IRtmpClientPeerContext>();
        }
    }

    public enum PublishingStreamResult
    {
        Succeeded,
        AlreadyExists,
        AlreadyPublishing,
    }

    public enum SubscribingStreamResult
    {
        Succeeded,
        AlreadySubscribing,
    }
}