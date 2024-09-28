﻿using LiveStreamingClientNet.Rtmp.Client.Contracts;
using LiveStreamingServerNet.Networking;
using LiveStreamingServerNet.Networking.Client.Contracts;
using LiveStreamingServerNet.Rtmp.Client.Configurations;
using LiveStreamingServerNet.Rtmp.Client.Contracts;
using LiveStreamingServerNet.Rtmp.Client.Exceptions;
using LiveStreamingServerNet.Rtmp.Client.Internal.Contracts;
using LiveStreamingServerNet.Rtmp.Client.Internal.Services.Contracts;
using LiveStreamingServerNet.Utilities.Contracts;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace LiveStreamingServerNet.Rtmp.Client.Internal
{
    internal class RtmpClient : IRtmpClient, IRtmpClientConnectionEventHandler
    {
        private readonly IClient _client;
        private readonly IRtmpCommanderService _commander;
        private readonly IRtmpProtocolControlService _protocolControl;
        private readonly IRtmpStreamFactory _streamFactory;
        private readonly RtmpClientConfiguration _config;

        private readonly CancellationTokenSource _clientCts = new();
        private readonly TaskCompletionSource _clientTcs = new();
        private readonly TaskCompletionSource _handshakeTcs = new();

        private Task? _clientTask;
        private int _connectOnce;

        public bool IsConnected { get; private set; }
        public bool IsHandshakeCompleted => _handshakeTcs.Task.IsCompletedSuccessfully;

        public bool IsStarted => _clientTask != null;
        public bool IsStopped => _clientTcs.Task.IsCompleted;

        public RtmpClient(
            IClient client,
            IRtmpCommanderService commander,
            IRtmpProtocolControlService protocolControl,
            IRtmpStreamFactory streamFactory,
            IOptions<RtmpClientConfiguration> config)
        {
            _client = client;
            _commander = commander;
            _protocolControl = protocolControl;
            _streamFactory = streamFactory;
            _config = config.Value;
        }

        public async Task<ConnectResponse> ConnectAsync(ServerEndPoint endPoint, string appName, IDictionary<string, object> information)
        {
            if (Interlocked.CompareExchange(ref _connectOnce, 1, 0) == 1)
                throw new InvalidOperationException("Connect method can be called only once.");

            try
            {
                _clientTask = RunClientAsync(endPoint);
                return await ConnectAfterHandshakeAsync(appName, information);
            }
            catch
            {
                _clientCts.Cancel();
                throw;
            }
        }

        public async Task<IRtmpStream> CreateStreamAsync()
        {
            if (!IsConnected)
                throw new InvalidOperationException("Client is not connected.");

            var createStreamTcs = new TaskCompletionSource<IRtmpStream>();

            _commander.CreateStream(
                callback: (success, streamContext) =>
                {
                    if (success && streamContext != null)
                    {
                        var stream = _streamFactory.Create(streamContext);
                        createStreamTcs.TrySetResult(stream);
                    }
                    else
                    {
                        createStreamTcs.TrySetException(new RtmpClientCommandException("Create stream failed."));
                    }

                    return ValueTask.CompletedTask;
                },
                cancellationCallback: () =>
                    createStreamTcs.TrySetException(new RtmpClientCommandException("Create stream failed."))
            );

            return await createStreamTcs.Task;
        }

        private async Task RunClientAsync(ServerEndPoint endPoint)
        {
            try
            {
                await _client.RunAsync(endPoint, _clientCts.Token);
                _clientTcs.TrySetResult();
            }
            catch (Exception ex)
            {
                _clientTcs.TrySetException(ex);
            }
            finally
            {
                IsConnected = false;
            }
        }

        private async Task<ConnectResponse> ConnectAfterHandshakeAsync(string appName, IDictionary<string, object> connectInformation)
        {
            await AwaitForHandshakeAsync();

            var connectTcs = new TaskCompletionSource<ConnectResponse>();

            _protocolControl.SetChunkSize(_config.OutChunkSize);
            _protocolControl.WindowAcknowledgementSize(_config.WindowAcknowledgementSize);

            _commander.Connect(appName, connectInformation,
                callback: (success, information, parameters) =>
                {
                    if (success)
                    {
                        IsConnected = true;
                        connectTcs.TrySetResult(new(new Dictionary<string, object>(information), parameters));
                    }
                    else
                    {
                        connectTcs.TrySetException(new RtmpClientConnectionException());
                    }

                    return ValueTask.CompletedTask;
                },
                cancellationCallback: () =>
                    connectTcs.TrySetException(new RtmpClientConnectionException())
            );

            return await connectTcs.Task;
        }

        private async Task AwaitForHandshakeAsync()
        {
            Debug.Assert(_clientTask != null);

            var timeoutTask = Task.Delay(_config.HandshakeTimeout, _clientCts.Token);
            var completedTask = await Task.WhenAny(_handshakeTcs.Task, _clientTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                throw new TimeoutException("Handshake timeout.");
            }
            else if (completedTask == _clientTask)
            {
                throw new RtmpClientConnectionException("Client connection failed.");
            }

            await completedTask;
        }

        public void Stop()
        {
            _clientCts.Cancel();
        }

        public async Task UntilStoppedAsync()
        {
            if (_clientTask != null)
            {
                await _clientTask;
            }
        }

        public ValueTask OnRtmpHandshakeCompleteAsync(IEventContext context)
        {
            _handshakeTcs.TrySetResult();
            return ValueTask.CompletedTask;
        }

        public ValueTask RtmpConnectedAsync(IEventContext context, IDictionary<string, object> commandObject, object? parameters)
            => ValueTask.CompletedTask;

        public ValueTask RtmpConnectionRejectedAsync(IEventContext context, IDictionary<string, object> commandObject, object? parameters)
            => ValueTask.CompletedTask;
    }
}
