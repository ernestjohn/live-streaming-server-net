﻿using LiveStreamingServerNet.Rtmp.Internal.Contracts;
using LiveStreamingServerNet.Utilities.Buffers.Contracts;

namespace LiveStreamingServerNet.Rtmp.Internal.RtmpEventHandlers.Commands.Dispatcher.Contracts
{
    internal interface IRtmpCommandDispatcher
    {
        ValueTask<bool> DispatchAsync(IRtmpChunkStreamContext chunkStreamContext, IRtmpClientContext clientContext, INetBuffer payloadBuffer, CancellationToken cancellationToken);
    }
}