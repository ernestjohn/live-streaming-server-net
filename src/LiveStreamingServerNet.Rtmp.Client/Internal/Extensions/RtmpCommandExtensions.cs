﻿namespace LiveStreamingServerNet.Rtmp.Client.Internal.Extensions
{
    using RtmpCommand = Client.Contracts.RtmpCommand;
    using RtmpCommandResponse = Client.Contracts.RtmpCommandResponse;
    using InternalRtmpCommand = Services.Contracts.RtmpCommand;
    using InternalRtmpCommandResponse = Services.Contracts.RtmpCommandResponse;

    internal static class RtmpCommandExtensions
    {
        public static InternalRtmpCommand ToInternal(this RtmpCommand command, uint messageStreamId)
        {
            return new(
                messageStreamId,
                command.ChunkStreamId,
                command.CommandName,
                command.CommandObject,
                command.Parameters,
                command.AmfEncodingType
            );
        }

        public static RtmpCommandResponse ToExternal(this InternalRtmpCommandResponse response)
        {
            return new(
                response.TransactionId,
                response.CommandObject,
                response.Parameters
            );
        }
    }
}
