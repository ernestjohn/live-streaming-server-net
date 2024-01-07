﻿using LiveStreamingServerNet.Newtorking;
using LiveStreamingServerNet.Rtmp.Internal.Contracts;
using MediatR;

namespace LiveStreamingServerNet.Rtmp.Internal.RtmpEvents
{
    internal record struct RtmpHandshakeC2Event(
        IRtmpClientContext ClientContext,
        ReadOnlyNetworkStream NetworkStream) : IRequest<bool>;
}