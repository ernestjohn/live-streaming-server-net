﻿using LiveStreamingServerNet.Rtmp;
using LiveStreamingServerNet.Transmuxer.Contracts;
using LiveStreamingServerNet.Utilities.Contracts;

namespace LiveStreamingServerNet.Transmuxer.Internal.Hls.Contracts
{
    internal interface IHlsTransmuxer : ITransmuxer
    {
        ValueTask OnReceiveMediaMessage(MediaType mediaType, IRentedBuffer rentedBuffer, uint timestamp);
    }
}
