﻿using LiveStreamingServerNet.Networking.Client.Installer.Contracts;
using LiveStreamingServerNet.Rtmp.Client.Installer.Contracts;

namespace LiveStreamingServerNet.Rtmp.Relay.Configurations
{
    public class RtmpUpstreamConfiguration
    {
        public bool Enabled { get; set; } = true;
        public TimeSpan MaximumIdleTime { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan IdleCheckingInterval { get; set; } = TimeSpan.FromSeconds(3);
        public Action<IRtmpClientConfigurator>? ConfigureRtmpUpstreamClient { get; set; } = null;
        public Action<IClientConfigurator>? ConfigureUpstreamClient { get; set; } = null;
    }
}
