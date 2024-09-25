﻿using LiveStreamingServerNet.Rtmp.Internal;
using LiveStreamingServerNet.Rtmp.Internal.RtmpEventHandlers;
using LiveStreamingServerNet.Rtmp.Internal.RtmpHeaders;
using LiveStreamingServerNet.Rtmp.Server.Internal.Contracts;
using LiveStreamingServerNet.Rtmp.Server.Internal.Services.Contracts;

namespace LiveStreamingServerNet.Rtmp.Server.Internal.Services
{
    internal class RtmpUserControlMessageSenderService : IRtmpUserControlMessageSenderService
    {
        private readonly IRtmpChunkMessageSenderService _chunkMessageSenderService;

        public RtmpUserControlMessageSenderService(IRtmpChunkMessageSenderService chunkMessageSenderService)
        {
            _chunkMessageSenderService = chunkMessageSenderService;
        }

        public void SendStreamBeginMessage(IRtmpSubscribeStreamContext subscribeStreamContext)
        {
            var basicHeader = new RtmpChunkBasicHeader(0, RtmpConstants.UserControlMessageChunkStreamId);
            var messageHeader = new RtmpChunkMessageHeaderType0(0, RtmpMessageType.UserControlMessage, RtmpConstants.UserControlMessageStreamId);

            _chunkMessageSenderService.Send(subscribeStreamContext.Stream.ClientContext, basicHeader, messageHeader, dataBuffer =>
            {
                dataBuffer.WriteUint16BigEndian(RtmpUserControlMessageTypes.StreamBegin);
                dataBuffer.WriteUInt32BigEndian(subscribeStreamContext.Stream.Id);
            });
        }

        public void SendStreamBeginMessage(IReadOnlyList<IRtmpSubscribeStreamContext> subscribeStreamContexts)
        {
            foreach (var subscribeStreamContextGroup in subscribeStreamContexts.GroupBy(x => x.Stream.Id))
            {
                var streamId = subscribeStreamContextGroup.Key;
                var clientContexts = subscribeStreamContextGroup.Select(x => x.Stream.ClientContext).ToList();

                var basicHeader = new RtmpChunkBasicHeader(0, RtmpConstants.UserControlMessageChunkStreamId);
                var messageHeader = new RtmpChunkMessageHeaderType0(0, RtmpMessageType.UserControlMessage, RtmpConstants.UserControlMessageStreamId);

                _chunkMessageSenderService.Send(clientContexts, basicHeader, messageHeader, dataBuffer =>
                {
                    dataBuffer.WriteUint16BigEndian(RtmpUserControlMessageTypes.StreamBegin);
                    dataBuffer.WriteUInt32BigEndian(streamId);
                });
            }
        }

        public void SendStreamEofMessage(IRtmpSubscribeStreamContext subscribeStreamContext)
        {
            var basicHeader = new RtmpChunkBasicHeader(0, RtmpConstants.UserControlMessageChunkStreamId);
            var messageHeader = new RtmpChunkMessageHeaderType0(0, RtmpMessageType.UserControlMessage, RtmpConstants.UserControlMessageStreamId);

            _chunkMessageSenderService.Send(subscribeStreamContext.Stream.ClientContext, basicHeader, messageHeader, dataBuffer =>
            {
                dataBuffer.WriteUint16BigEndian(RtmpUserControlMessageTypes.StreamEof);
                dataBuffer.WriteUInt32BigEndian(subscribeStreamContext.Stream.Id);
            });
        }

        public void SendStreamEofMessage(IReadOnlyList<IRtmpSubscribeStreamContext> subscribeStreamContexts)
        {
            foreach (var subscribeStreamContextGroup in subscribeStreamContexts.GroupBy(x => x.Stream.Id))
            {
                var streamId = subscribeStreamContextGroup.Key;
                var clientContexts = subscribeStreamContextGroup.Select(x => x.Stream.ClientContext).ToList();

                var basicHeader = new RtmpChunkBasicHeader(0, RtmpConstants.UserControlMessageChunkStreamId);
                var messageHeader = new RtmpChunkMessageHeaderType0(0, RtmpMessageType.UserControlMessage, RtmpConstants.UserControlMessageStreamId);

                _chunkMessageSenderService.Send(clientContexts, basicHeader, messageHeader, dataBuffer =>
                {
                    dataBuffer.WriteUint16BigEndian(RtmpUserControlMessageTypes.StreamEof);
                    dataBuffer.WriteUInt32BigEndian(streamId);
                });
            }
        }
    }
}