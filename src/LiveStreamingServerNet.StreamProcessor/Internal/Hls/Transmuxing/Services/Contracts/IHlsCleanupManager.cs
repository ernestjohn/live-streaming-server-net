﻿using LiveStreamingServerNet.StreamProcessor.Internal.Containers;

namespace LiveStreamingServerNet.StreamProcessor.Internal.Hls.Transmuxing.Services.Contracts
{
    internal interface IHlsCleanupManager
    {
        ValueTask ExecuteCleanupAsync(string manifestPath);
        ValueTask ScheduleCleanupAsync(string manifestPath, IList<TsSegment> tsSegments, TimeSpan cleanupDelay);
    }
}