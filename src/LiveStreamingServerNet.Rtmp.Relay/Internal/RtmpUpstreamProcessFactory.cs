﻿using LiveStreamingServerNet.Rtmp.Relay.Configurations;
using LiveStreamingServerNet.Rtmp.Relay.Contracts;
using LiveStreamingServerNet.Rtmp.Relay.Internal.Contracts;
using LiveStreamingServerNet.Utilities.Buffers.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LiveStreamingServerNet.Rtmp.Relay.Internal
{
    internal class RtmpUpstreamProcessFactory : IRtmpUpstreamProcessFactory
    {
        private readonly IRtmpOriginResolver _originResolver;
        private readonly IBufferPool _bufferPool;
        private readonly IDataBufferPool _dataBufferPool;
        private readonly IOptions<RtmpUpstreamConfiguration> _config;
        private readonly ILogger<RtmpUpstreamProcess> _logger;

        public RtmpUpstreamProcessFactory(
            IRtmpOriginResolver originResolver,
            IBufferPool bufferPool,
            IDataBufferPool dataBufferPool,
            IOptions<RtmpUpstreamConfiguration> config,
            ILogger<RtmpUpstreamProcess> logger)
        {
            _originResolver = originResolver;
            _bufferPool = bufferPool;
            _dataBufferPool = dataBufferPool;
            _config = config;
            _logger = logger;
        }

        public IRtmpUpstreamProcess Create(string streamPath)
        {
            return new RtmpUpstreamProcess(
                streamPath,
                _originResolver,
                _bufferPool,
                _dataBufferPool,
                _config,
                _logger);
        }
    }
}
