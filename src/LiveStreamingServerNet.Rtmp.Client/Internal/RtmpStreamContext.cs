﻿using LiveStreamingServerNet.Rtmp.Client.Contracts;
using LiveStreamingServerNet.Rtmp.Client.Internal.Contracts;

namespace LiveStreamingServerNet.Rtmp.Client.Internal
{
    internal class RtmpStreamContext : IRtmpStreamContext
    {
        public uint StreamId { get; }

        public IRtmpSessionContext SessionContext { get; }
        public IRtmpPublishStreamContext? PublishContext { get; private set; }
        public IRtmpSubscribeStreamContext? SubscribeContext { get; private set; }

        public event EventHandler<IRtmpPublishStreamContext>? OnPublishContextCreated;
        public event EventHandler<IRtmpSubscribeStreamContext>? OnSubscribeContextCreated;
        public event EventHandler<IRtmpPublishStreamContext>? OnPublishContextRemoved;
        public event EventHandler<IRtmpSubscribeStreamContext>? OnSubscribeContextRemoved;

        public RtmpStreamContext(uint streamId, IRtmpSessionContext sessionContext)
        {
            StreamId = streamId;
            SessionContext = sessionContext;
        }

        public IRtmpPublishStreamContext CreatePublishContext()
        {
            ValidateContextCreation();

            PublishContext = new RtmpPublishStreamContext(this);
            OnPublishContextCreated?.Invoke(this, PublishContext);
            return PublishContext;
        }

        public IRtmpSubscribeStreamContext CreateSubscribeContext()
        {
            ValidateContextCreation();

            SubscribeContext = new RtmpSubscribeStreamContext(this);
            OnSubscribeContextCreated?.Invoke(this, SubscribeContext);
            return SubscribeContext;
        }

        public void RemovePublishContext()
        {
            if (PublishContext != null)
            {
                OnPublishContextRemoved?.Invoke(this, PublishContext);

                PublishContext.Dispose();
                PublishContext = null;
            }
        }

        public void RemoveSubscribeContext()
        {
            if (SubscribeContext != null)
            {
                OnSubscribeContextRemoved?.Invoke(this, SubscribeContext);

                SubscribeContext.Dispose();
                SubscribeContext = null;
            }
        }

        private void ValidateContextCreation()
        {
            if (PublishContext != null)
                throw new InvalidOperationException("Publish context already exists.");

            if (SubscribeContext != null)
                throw new InvalidOperationException("Subscribe context already exists.");
        }

        public void Dispose()
        {
            RemovePublishContext();
            RemoveSubscribeContext();
        }
    }

    internal abstract class RtmpMediaStreamContext : IRtmpMediaStreamContext
    {
        public IRtmpStreamContext StreamContext { get; }

        protected RtmpMediaStreamContext(IRtmpStreamContext streamContext)
        {
            StreamContext = streamContext;
        }

        public virtual void Dispose() { }
    }

    internal class RtmpPublishStreamContext : RtmpMediaStreamContext, IRtmpPublishStreamContext
    {
        public RtmpPublishStreamContext(IRtmpStreamContext streamContext) : base(streamContext) { }
    }

    internal class RtmpSubscribeStreamContext : RtmpMediaStreamContext, IRtmpSubscribeStreamContext
    {
        private IReadOnlyDictionary<string, object>? _streamMetaData;

        public event EventHandler<StreamMetaDataEventArgs>? OnStreamMetaDataReceived;
        public event EventHandler<MediaDataEventArgs>? OnVideoDataReceived;
        public event EventHandler<MediaDataEventArgs>? OnAudioDataReceived;
        public event EventHandler<StatusEventArgs>? OnStatusReceived;
        public event EventHandler<UserControlEventArgs>? OnUserControlEventReceived;

        public RtmpSubscribeStreamContext(IRtmpStreamContext streamContext) : base(streamContext) { }

        public IReadOnlyDictionary<string, object>? StreamMetaData
        {
            get => _streamMetaData;
            set
            {
                _streamMetaData = value;

                if (value != null)
                {
                    OnStreamMetaDataReceived?.Invoke(this, new StreamMetaDataEventArgs(value));
                }
            }
        }

        public void ReceiveVideoData(MediaDataEventArgs eventArgs)
        {
            OnVideoDataReceived?.Invoke(this, eventArgs);
        }

        public void ReceiveAudioData(MediaDataEventArgs eventArgs)
        {
            OnAudioDataReceived?.Invoke(this, eventArgs);
        }

        public void ReceiveStatus(StatusEventArgs eventArgs)
        {
            OnStatusReceived?.Invoke(this, eventArgs);
        }

        public void ReceiveUserControlEvent(UserControlEventArgs eventArgs)
        {
            OnUserControlEventReceived?.Invoke(this, eventArgs);
        }
    }
}
