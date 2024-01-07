﻿using LiveStreamingServerNet.Rtmp.Internal.Contracts;
using LiveStreamingServerNet.Rtmp.Internal.Extensions;
using LiveStreamingServerNet.Rtmp.Internal.RtmpEventHandlers;
using LiveStreamingServerNet.Rtmp.Internal.RtmpHeaders;
using LiveStreamingServerNet.Rtmp.Internal.Services.Contracts;

namespace LiveStreamingServerNet.Rtmp.Internal.Services
{
    internal class RtmpProtocolControlMessageSenderService : IRtmpProtocolControlMessageSenderService
    {
        private readonly IRtmpChunkMessageSenderService _chunkMessageSenderService;

        public RtmpProtocolControlMessageSenderService(IRtmpChunkMessageSenderService chunkMessageSenderService)
        {
            _chunkMessageSenderService = chunkMessageSenderService;
        }

        public void SetChunkSize(IRtmpClientContext clientContext, uint chunkSize)
        {
            var basicHeader = new RtmpChunkBasicHeader(0, RtmpConstants.ProtocolControlMessageChunkStreamId);
            var messageHeader = new RtmpChunkMessageHeaderType0(0, RtmpMessageType.SetChunkSize, RtmpConstants.ProtocolControlMessageStreamId);

            _chunkMessageSenderService.Send(clientContext, basicHeader, messageHeader, netBuffer =>
            {
                netBuffer.WriteUInt32BigEndian(chunkSize);
            });

            clientContext.OutChunkSize = chunkSize;
        }

        public void AbortMessage(IRtmpClientContext clientContext, uint streamId)
        {
            var basicHeader = new RtmpChunkBasicHeader(0, RtmpConstants.ProtocolControlMessageChunkStreamId);
            var messageHeader = new RtmpChunkMessageHeaderType0(0, RtmpMessageType.AbortMessage, RtmpConstants.ProtocolControlMessageStreamId);

            _chunkMessageSenderService.Send(clientContext, basicHeader, messageHeader, netBuffer =>
            {
                netBuffer.WriteUInt32BigEndian(streamId);
            });
        }

        public void Acknowledgement(IRtmpClientContext clientContext, uint sequenceNumber)
        {
            var basicHeader = new RtmpChunkBasicHeader(0, RtmpConstants.ProtocolControlMessageChunkStreamId);
            var messageHeader = new RtmpChunkMessageHeaderType0(0, RtmpMessageType.Acknowledgement, RtmpConstants.ProtocolControlMessageStreamId);

            _chunkMessageSenderService.Send(clientContext, basicHeader, messageHeader, netBuffer =>
            {
                netBuffer.WriteUInt32BigEndian(sequenceNumber);
            });
        }

        public void WindowAcknowledgementSize(IRtmpClientContext clientContext, uint acknowledgementWindowSize)
        {
            var basicHeader = new RtmpChunkBasicHeader(0, RtmpConstants.ProtocolControlMessageChunkStreamId);
            var messageHeader = new RtmpChunkMessageHeaderType0(0, RtmpMessageType.WindowAcknowledgementSize, RtmpConstants.ProtocolControlMessageStreamId);

            _chunkMessageSenderService.Send(clientContext, basicHeader, messageHeader, netBuffer =>
            {
                netBuffer.WriteUInt32BigEndian(acknowledgementWindowSize);
            });

            clientContext.OutWindowAcknowledgementSize = acknowledgementWindowSize;
        }

        public void SetPeerBandwith(IRtmpClientContext clientContext, uint peerBandwith, RtmpPeerBandwithLimitType limitType)
        {
            var basicHeader = new RtmpChunkBasicHeader(0, RtmpConstants.ProtocolControlMessageChunkStreamId);
            var messageHeader = new RtmpChunkMessageHeaderType0(0, RtmpMessageType.SetPeerBandwith, RtmpConstants.ProtocolControlMessageStreamId);

            _chunkMessageSenderService.Send(clientContext, basicHeader, messageHeader, netBuffer =>
            {
                netBuffer.WriteUInt32BigEndian(peerBandwith);
                netBuffer.Write((byte)limitType);
            });
        }
    }
}
