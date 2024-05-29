﻿using LiveStreamingServerNet.StreamProcessor.Internal.Hls.Contracts;
using LiveStreamingServerNet.StreamProcessor.Internal.Hls.Services.Contracts;
using System.Collections.Concurrent;

namespace LiveStreamingServerNet.StreamProcessor.Internal.Hls.Services
{
    internal class HlsUploadingManager : IHlsUploadingManager, IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, UploaderTask> _uploaderTasks;
        private readonly IHlsUploaderFactory _uploaderFactory;

        public HlsUploadingManager(IHlsUploaderFactory uploaderFactory)
        {
            _uploaderTasks = new ConcurrentDictionary<string, UploaderTask>();
            _uploaderFactory = uploaderFactory;
        }

        public Task StartUploading(StreamProcessingContext context)
        {
            var cts = new CancellationTokenSource();
            var uploader = _uploaderFactory.Create(context);

            var uploaderTask = uploader.RunAsync(cts.Token);
            _uploaderTasks[context.OutputPath] = new UploaderTask(uploaderTask, cts);
            _ = uploaderTask.ContinueWith(_ => _uploaderTasks.TryRemove(context.OutputPath, out var _));

            return Task.CompletedTask;
        }

        public Task StopUploading(StreamProcessingContext context)
        {
            if (_uploaderTasks.TryGetValue(context.OutputPath, out var uploaderTask))
                uploaderTask.Cts.Cancel();

            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            await Task.WhenAll(_uploaderTasks.Values.Select(t => t.Task));
        }

        private record UploaderTask(Task Task, CancellationTokenSource Cts);
    }
}