﻿using LiveStreamingServerNet.Utilities.Buffers.Contracts;

namespace LiveStreamingServerNet.Rtmp.Internal.RtmpEventHandlers.Handshakes
{
    internal class SimpleHandshake
    {
        private const byte _clientType = 3;

        private readonly INetBuffer _incomingBuffer;

        public SimpleHandshake(INetBuffer incomingBuffer)
        {
            _incomingBuffer = incomingBuffer;
        }

        public bool ValidateC1()
        {
            return true;
        }

        public void WriteS0S1S2(INetBuffer outgoingBuffer)
        {
            WriteS0(outgoingBuffer);
            WriteS1(outgoingBuffer);
            WriteS2(outgoingBuffer);
        }

        public void WriteS0(INetBuffer outgoingBuffer)
        {
            outgoingBuffer.Write(_clientType);
        }

        public void WriteS1(INetBuffer outgoingBuffer)
        {
            outgoingBuffer.Write(HandshakeUtilities.GetTime());
            outgoingBuffer.Write(RtmpConstants.ServerVersion);
            outgoingBuffer.WriteRandomBytes(1536 - 8);
        }

        public void WriteS2(INetBuffer outgoingBuffer)
        {
            _incomingBuffer.CopyAllTo(outgoingBuffer);
        }
    }
}
