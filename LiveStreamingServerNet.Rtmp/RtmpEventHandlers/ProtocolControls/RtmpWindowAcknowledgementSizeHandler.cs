﻿using LiveStreamingServerNet.Newtorking.Contracts;
using LiveStreamingServerNet.Rtmp.Contracts;
using LiveStreamingServerNet.Rtmp.Extensions;
using LiveStreamingServerNet.Rtmp.Logging;
using LiveStreamingServerNet.Rtmp.RtmpEventHandlers.MessageDispatcher.Attributes;
using LiveStreamingServerNet.Rtmp.RtmpEventHandlers.MessageDispatcher.Contracts;
using Microsoft.Extensions.Logging;

namespace LiveStreamingServerNet.Rtmp.RtmpEventHandlers.ProtocolControls
{
    [RtmpMessageType(RtmpMessageType.WindowAcknowledgementSize)]
    internal class RtmpWindowAcknowledgementSizeHandler : IRtmpMessageHandler
    {
        private readonly ILogger _logger;

        public RtmpWindowAcknowledgementSizeHandler(ILogger<RtmpWindowAcknowledgementSizeHandler> logger)
        {
            _logger = logger;
        }

        public Task<bool> HandleAsync(
            IRtmpChunkStreamContext chunkStreamContext,
            IRtmpClientPeerContext peerContext,
            INetBuffer payloadBuffer,
            CancellationToken cancellationToken)
        {
            peerContext.InWindowAcknowledgementSize = payloadBuffer.ReadUInt32BigEndian();
            _logger.WindowAcknowledgementSize(peerContext.Peer.PeerId, peerContext.InWindowAcknowledgementSize);
            return Task.FromResult(true);
        }
    }
}