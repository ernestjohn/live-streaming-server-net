﻿using k8s;
using k8s.Models;
using LiveStreamingServerNet.KubernetesPod.Configurations;
using LiveStreamingServerNet.KubernetesPod.Internal.Logging;
using LiveStreamingServerNet.KubernetesPod.Internal.Services.Contracts;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Serialization;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace LiveStreamingServerNet.KubernetesPod.Internal.Services
{
    internal class KubernetesContext : IKubernetesContext
    {
        public Kubernetes KubernetesClient { get; }
        public string PodNamespace { get; }
        public string PodName { get; }

        private readonly KubernetesPodConfiguration _config;
        private readonly ILogger _logger;

        public KubernetesContext(IOptions<KubernetesPodConfiguration> config, ILogger<KubernetesContext> logger)
        {
            _config = config.Value;
            _logger = logger;

            KubernetesClient = CreateKubernetesClient();
            PodNamespace = GetPodNamespace();
            PodName = GetPodName();
        }

        private Kubernetes CreateKubernetesClient()
        {
            var kubeConfig = KubernetesClientConfiguration.InClusterConfig();
            return new Kubernetes(kubeConfig);
        }

        private string GetPodNamespace()
        {
            return Environment.GetEnvironmentVariable(_config.PodNamespaceEnvironmentVariableName) ??
                throw new InvalidOperationException($"Environment variable '{_config.PodNamespaceEnvironmentVariableName}' is not set.");
        }

        private string GetPodName()
        {
            return Environment.GetEnvironmentVariable(_config.PodNameEnvironmentVariableName) ??
                throw new InvalidOperationException($"Environment variable '{_config.PodNameEnvironmentVariableName}' is not set.");
        }

        public async Task<V1Pod> GetPodAsync(CancellationToken cancellationToken)
        {
            return (await KubernetesClient.CoreV1.ListNamespacedPodAsync(
                namespaceParameter: PodNamespace,
                fieldSelector: $"metadata.name={PodName}",
                cancellationToken: cancellationToken
            )).Items.FirstOrDefault() ??
                throw new InvalidOperationException($"Failed to get the pod '{PodName} in {PodNamespace}");
        }

        public async IAsyncEnumerable<(WatchEventType, V1Pod)> WatchPodAsync(
            [EnumeratorCancellation] CancellationToken stoppingToken = default,
            TimeSpan? reconnectCheck = null)
        {
            reconnectCheck ??= TimeSpan.FromMinutes(5);

            while (true)
            {
                stoppingToken.ThrowIfCancellationRequested();

                var lastEventReceivedTime = DateTime.UtcNow;

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                var cancellationToken = cts.Token;

                using var timer = new Timer(_ =>
                {
                    if (cts.IsCancellationRequested)
                        return;

                    var timeSinceLastEvent = DateTime.UtcNow - lastEventReceivedTime;
                    if (timeSinceLastEvent > reconnectCheck.Value)
                    {
                        _logger.RestartingWatcher(lastEventReceivedTime);
                        cts.Cancel();
                    }
                },
                state: null,
                dueTime: reconnectCheck.Value / 2,
                period: reconnectCheck.Value / 2);

                IAsyncEnumerator<(WatchEventType, V1Pod)> watcher;

                try
                {
                    watcher = WatchPodAsyncCore(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    continue;
                }

                while (true)
                {
                    try
                    {
                        if (!await watcher.MoveNextAsync())
                            break;
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    lastEventReceivedTime = DateTime.UtcNow;
                    yield return watcher.Current;
                }
            }
        }

        private IAsyncEnumerator<(WatchEventType, V1Pod)> WatchPodAsyncCore(CancellationToken cancellationToken)
        {
            var watcher = KubernetesClient.CoreV1.ListNamespacedPodWithHttpMessagesAsync(
                namespaceParameter: PodNamespace,
                fieldSelector: $"metadata.name={PodName}",
                watch: true,
                cancellationToken: cancellationToken
            ).WatchAsync<V1Pod, V1PodList>(
                onError: ex =>
                {
                    if (ex is KubernetesException kubernetesError &&
                        string.Equals(kubernetesError.Status.Reason, "Expired", StringComparison.Ordinal))
                    {
                        throw ex;
                    }

                    _logger.IgnoringWatchingPodError(ex);
                },
                cancellationToken: cancellationToken
            );

            return watcher.GetAsyncEnumerator(cancellationToken);
        }

        public async Task PatchPodAsync(Action<IPodPatcherBuilder> configureBuilder)
        {
            var builder = new PodPatcherBuilder();
            configureBuilder.Invoke(builder);
            await PatchPodAsync(builder.Build());
        }

        private async Task PatchPodAsync(JsonPatchDocument<V1Pod> doc)
        {
            var jsonPatch = JsonSerializer.Serialize(
                doc.Operations.Select(o => new
                {
                    o.op,
                    o.path,
                    o.value
                })
            );

            try
            {
                var patch = new V1Patch(jsonPatch, V1Patch.PatchType.JsonPatch);
                await KubernetesClient.CoreV1.PatchNamespacedPodAsync(patch, PodName, PodNamespace);
            }
            catch (Exception ex)
            {
                _logger.ErrorPatchingPod(jsonPatch, ex);
                throw;
            }
        }

        internal class PodPatcherBuilder : IPodPatcherBuilder
        {
            private readonly JsonPatchDocument<V1Pod> _doc;

            public PodPatcherBuilder()
            {
                _doc = new JsonPatchDocument<V1Pod>();
                _doc.ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() };
            }

            public IPodPatcherBuilder SetLabel(string key, string value)
            {
                _doc.Replace(x => x.Metadata.Labels[key], value);
                return this;
            }

            public IPodPatcherBuilder RemoveLabel(string key)
            {
                _doc.Remove(x => x.Metadata.Labels[key]);
                return this;
            }

            public IPodPatcherBuilder SetAnnotation(string key, string value)
            {
                _doc.Replace(x => x.Metadata.Annotations[key], value);
                return this;
            }

            public IPodPatcherBuilder RemoveAnnotation(string key)
            {
                _doc.Remove(x => x.Metadata.Annotations[key]);
                return this;
            }

            public JsonPatchDocument<V1Pod> Build()
            {
                return _doc;
            }
        }
    }
}
