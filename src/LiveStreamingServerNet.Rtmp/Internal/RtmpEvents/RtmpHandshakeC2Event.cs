﻿using LiveStreamingServerNet.Networking;
using LiveStreamingServerNet.Rtmp.Internal.Contracts;
using MediatR;

namespace LiveStreamingServerNet.Rtmp.Internal.RtmpEvents
{
    internal record struct RtmpHandshakeC2Event(
        IRtmpClientContext ClientContext,
        ReadOnlyStream NetworkStream) : IRequest<bool>;
}
