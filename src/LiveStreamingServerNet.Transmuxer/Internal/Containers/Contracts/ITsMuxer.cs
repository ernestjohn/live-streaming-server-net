﻿namespace LiveStreamingServerNet.Transmuxer.Internal.Containers.Contracts
{
    internal interface ITsMuxer : IDisposable
    {
        int BufferSize { get; }
        uint SequenceNumber { get; }

        ValueTask<TsSegment?> FlushAsync(uint timestamp);
        void SetAACSequenceHeader(AACSequenceHeader aacSequenceHeader);
        void SetAVCSequenceHeader(AVCSequenceHeader avcSequenceHeader);
        bool WriteAudioPacket(ArraySegment<byte> buffer, uint timestamp);
        bool WriteVideoPacket(ArraySegment<byte> dataBuffer, uint timestamp, uint compositionTime, bool isKeyFrame);
    }
}