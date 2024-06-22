﻿using LiveStreamingServerNet.Rtmp.Internal.Contracts;
using LiveStreamingServerNet.Rtmp.Internal.Logging;
using LiveStreamingServerNet.Rtmp.Internal.RtmpEvents;
using LiveStreamingServerNet.Utilities.Buffers.Contracts;
using Mediator;
using Microsoft.Extensions.Logging;

namespace LiveStreamingServerNet.Rtmp.Internal.RtmpEventHandlers
{
    internal class RtmpHandshakeC2EventHandler : IRequestHandler<RtmpHandshakeC2Event, RtmpEventConsumingResult>
    {
        private readonly INetBufferPool _netBufferPool;
        private readonly IRtmpServerConnectionEventDispatcher _eventDispatcher;
        private readonly ILogger _logger;

        private const int HandshakeC2Size = 1536;

        public RtmpHandshakeC2EventHandler(
            INetBufferPool netBufferPool,
            IRtmpServerConnectionEventDispatcher eventDispatcher,
            ILogger<RtmpHandshakeC2EventHandler> logger)
        {
            _netBufferPool = netBufferPool;
            _eventDispatcher = eventDispatcher;
            _logger = logger;
        }

        // todo: add validation
        public async ValueTask<RtmpEventConsumingResult> Handle(RtmpHandshakeC2Event @event, CancellationToken cancellationToken)
        {
            var incomingBuffer = _netBufferPool.Obtain();

            try
            {
                await incomingBuffer.FromStreamData(@event.NetworkStream, HandshakeC2Size, cancellationToken);

                @event.ClientContext.State = RtmpClientState.HandshakeDone;

                _logger.HandshakeC2Handled(@event.ClientContext.Client.ClientId);

                await _eventDispatcher.RtmpClientHandshakeCompleteAsync(@event.ClientContext);

                return new RtmpEventConsumingResult(true, HandshakeC2Size);
            }
            finally
            {
                _netBufferPool.Recycle(incomingBuffer);
            }
        }
    }
}
