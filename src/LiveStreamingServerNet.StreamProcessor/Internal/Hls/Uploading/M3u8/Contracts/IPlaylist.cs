﻿using LiveStreamingServerNet.StreamProcessor.Hls;

namespace LiveStreamingServerNet.StreamProcessor.Internal.Hls.Uploading.M3u8.Contracts
{
    internal interface IPlaylist : IManifestContainer, ITsSegmentsContainer
    {
        bool IsMaster { get; }
        Manifest Manifest { get; }
    }

    internal interface IManifestContainer
    {
        IReadOnlyDictionary<string, Manifest> Manifests { get; }
    }

    internal interface ITsSegmentsContainer
    {
        IReadOnlyList<ManifestTsSegment> TsSegments { get; }
    }
}
