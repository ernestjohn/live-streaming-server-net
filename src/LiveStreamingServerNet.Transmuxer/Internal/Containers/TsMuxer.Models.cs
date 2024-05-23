﻿using LiveStreamingServerNet.Networking.Contracts;
using System.Diagnostics;
using System.IO.Hashing;
using System.Runtime.CompilerServices;

namespace LiveStreamingServerNet.Transmuxer.Internal.Containers
{
    internal partial class TsMuxer
    {
        private record struct TransportStreamHeader(
            bool IsFirst, ushort PacketId, bool HasPayload, byte ContinuityCounter)
        {
            public int Size => 4;
            public bool HasAdaptionField { get; set; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Write(INetBuffer netBuffer)
            {
                WriteSyncByte(netBuffer);
                WriteUnitStartIndicatorAndPacketId(netBuffer);
                WriteContinuityCounterAndAdaptationFieldControl(netBuffer);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void WriteSyncByte(INetBuffer netBuffer)
            {
                netBuffer.Write(TsConstants.SyncByte);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void WriteUnitStartIndicatorAndPacketId(INetBuffer netBuffer)
            {
                var firstByte = (byte)(PacketId >> 8);
                var secondByte = (byte)(PacketId & 0xff);

                if (IsFirst) firstByte |= 0x40;

                netBuffer.Write(firstByte);
                netBuffer.Write(secondByte);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void WriteContinuityCounterAndAdaptationFieldControl(
                INetBuffer netBuffer)
            {
                var @byte = (byte)(ContinuityCounter & 0xf);
                if (HasPayload) @byte |= 0x10;
                if (HasAdaptionField) @byte |= 0x20;

                netBuffer.Write(@byte);
            }
        }

        public record struct AdaptionField(int? DecodingTimstamp)
        {
            public const int BaseSize = 2;
            public int Size => (Present ? BaseSize : 0) + (DecodingTimstamp.HasValue ? 6 : 0) + (StuffingSize ?? 0);
            public int? StuffingSize { get; set; }
            public bool Present => DecodingTimstamp.HasValue || StuffingSize.HasValue;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Write(INetBuffer netBuffer)
            {
                if (!Present) return;

                WriteSizeAndFlags(netBuffer);
                WritePcr(netBuffer);
                WriteStuffing(netBuffer);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void WriteSizeAndFlags(INetBuffer netBuffer)
            {
                var size = (byte)(Size - 1);

                // Random Access Indicator + PCR Flag
                var flags = (byte)(DecodingTimstamp.HasValue ? 0x50 : 0x00);

                netBuffer.Write(size);
                netBuffer.Write(flags);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void WritePcr(INetBuffer netBuffer)
            {
                if (!DecodingTimstamp.HasValue) return;

                netBuffer.Write((byte)(DecodingTimstamp.Value >> 25));
                netBuffer.Write((byte)(DecodingTimstamp.Value >> 17));
                netBuffer.Write((byte)(DecodingTimstamp.Value >> 9));
                netBuffer.Write((byte)(DecodingTimstamp.Value >> 1));
                netBuffer.Write((byte)(((DecodingTimstamp.Value & 0x1) << 7) | 0x7e));
                netBuffer.Write((byte)0x00);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void WriteStuffing(INetBuffer netBuffer)
            {
                for (var i = 0; i < StuffingSize; i++)
                    netBuffer.Write(TsConstants.StuffingByte);
            }
        }

        private record struct PacketizedElementaryStreamHeader(byte StreamId, int DecodingTimstamp, int PresentationTimestamp, int DataSize)
        {
            public int Size => 14 + (DecodingTimstamp != PresentationTimestamp ? 5 : 0);

            public void Write(INetBuffer netBuffer)
            {
                WritePrefix(netBuffer);
                WriteStreamId(netBuffer);
                WritePacketSizeAndFlagsAndTimestamps(netBuffer);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void WritePrefix(INetBuffer netBuffer)
            {
                netBuffer.Write((byte)0x00);
                netBuffer.Write((byte)0x00);
                netBuffer.Write((byte)0x01);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void WriteStreamId(INetBuffer netBuffer)
            {
                netBuffer.Write(StreamId);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void WritePacketSizeAndFlagsAndTimestamps(INetBuffer netBuffer)
            {
                var headerSize = 5;
                var flags = 0x80;

                bool writeDts = DecodingTimstamp != PresentationTimestamp;
                if (writeDts)
                {
                    headerSize += 5;
                    flags |= 0x40;
                }

                var packetSize = DataSize + headerSize + 3;

                if (packetSize > 0xffff)
                    packetSize = 0;

                netBuffer.Write((byte)(packetSize >> 8));
                netBuffer.Write((byte)packetSize);

                netBuffer.Write((byte)0x80);
                netBuffer.Write((byte)flags);
                netBuffer.Write((byte)headerSize);

                WriteTimestamp(netBuffer, (byte)(flags >> 6), PresentationTimestamp);
                if (writeDts) WriteTimestamp(netBuffer, 1, DecodingTimstamp);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void WriteTimestamp(INetBuffer netBuffer, byte flags, int timestamp)
            {
                int val;

                val = flags << 4 | (((timestamp >> 30) & 0x07) << 1) | 1;
                netBuffer.Write((byte)val);

                val = (((timestamp >> 15) & 0x7fff) << 1) | 1;
                netBuffer.Write((byte)(val >> 8));
                netBuffer.Write((byte)val);

                val = ((timestamp & 0x7fff) << 1) | 1;
                netBuffer.Write((byte)(val >> 8));
                netBuffer.Write((byte)val);
            }
        }

        private record struct AudioDataTransportStreamHeader(AACSequenceHeader SequenceHeader, int DataSize)
        {
            public const int Size = 7;

            public void FillBuffer(byte[] buffer)
            {
                Debug.Assert(buffer.Length >= Size);

                var payloadLength = DataSize + Size;

                buffer[0] = 0xff;
                buffer[1] = 0xf1;
                buffer[2] = (byte)((SequenceHeader.ObjectType - 1) << 6 | SequenceHeader.SampleRateIndex << 2);
                buffer[3] = (byte)(SequenceHeader.ChannelConfig << 6 | (payloadLength >> 11) & 0x3);
                buffer[4] = (byte)(payloadLength >> 3);
                buffer[5] = (byte)(0x1f | payloadLength << 5);
                buffer[6] = 0xfc;
            }
        }

        private record struct ProgramSpecificInformationHeader(byte TableId, int DataSize)
        {
            public int Size => 4;
            public int ChecksumOffset => 1;

            public void Write(INetBuffer netBuffer)
            {
                WritePointerField(netBuffer);
                WriteTableHeader(netBuffer);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void WritePointerField(INetBuffer netBuffer)
            {
                netBuffer.Write((byte)0);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void WriteTableHeader(INetBuffer netBuffer)
            {
                netBuffer.Write(TableId);
                netBuffer.WriteUint16BigEndian((ushort)(0x1 << 15 | 0x3 << 12 | (DataSize + TableChecksum.Size)));
            }
        }

        private record struct ProgramAssociationTable(ushort TransportStreamIdentifier, ushort ProgramNumber, ushort ProgramMapPID)
        {
            public int Size => 2 + 3 + 4;

            public void Write(INetBuffer netBuffer)
            {
                WriteTablePrefix(netBuffer);
                WriteTable(netBuffer);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void WriteTablePrefix(INetBuffer netBuffer)
            {
                netBuffer.WriteUint16BigEndian(TransportStreamIdentifier);
                netBuffer.Write((byte)((0x3 << 6) | 1));
                netBuffer.Write((byte)0x00);
                netBuffer.Write((byte)0x00);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void WriteTable(INetBuffer netBuffer)
            {
                netBuffer.WriteUint16BigEndian(ProgramNumber);
                netBuffer.WriteUint16BigEndian((ushort)(0xe000 | ProgramMapPID));
            }
        }

        private record struct ElementaryStreamInfo(byte StreamType, ushort ElementaryPID)
        {
            public int Size => 5;

            public void Write(INetBuffer netBuffer)
            {
                netBuffer.Write(StreamType);
                netBuffer.WriteUint16BigEndian((ushort)(0x07 << 13 | ElementaryPID));
                netBuffer.Write((byte)0x00);
                netBuffer.Write((byte)0x00);
            }
        }

        private record struct ProgramMapTable(ushort PrcPID, 

        private record struct TableChecksum(byte[] Buffer, int Start, int Length)
        {
            public const int Size = 4;

            public void Write(INetBuffer netBuffer)
            {
                var checksum = Crc32.HashToUInt32(Buffer.AsSpan(Start, Length));
                netBuffer.WriteUInt32BigEndian(checksum);
            }
        }
    }
}