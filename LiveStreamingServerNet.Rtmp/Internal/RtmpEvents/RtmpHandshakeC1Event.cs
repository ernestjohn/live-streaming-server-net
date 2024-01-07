﻿using LiveStreamingServerNet.Newtorking;
using LiveStreamingServerNet.Rtmp.Internal.Contracts;
using MediatR;

namespace LiveStreamingServerNet.Rtmp.Internal.RtmpEvents
{
    internal record struct RtmpHandshakeC1Event(
        IRtmpClientContext ClientContext,
        ReadOnlyNetworkStream NetworkStream) : IRequest<bool>;
}