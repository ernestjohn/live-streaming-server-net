﻿using LiveStreamingServerNet.Newtorking;
using LiveStreamingServerNet.Rtmp.Internal.Contracts;
using LiveStreamingServerNet.Rtmp.Internal.RtmpEvents;
using MediatR;

namespace LiveStreamingServerNet.Rtmp.Internal
{
    internal class RtmpClientHandler : IRtmpClientHandler
    {
        private readonly IMediator _mediator;
        private readonly IRtmpServerConnectionEventDispatcher _eventDispatcher;

        private IRtmpClientContext _clientContext = default!;

        public RtmpClientHandler(IMediator mediator, IRtmpServerConnectionEventDispatcher eventDispatcher)
        {
            _mediator = mediator;
            _eventDispatcher = eventDispatcher;
        }

        public async Task InitializeAsync(IRtmpClientContext clientContext)
        {
            _clientContext = clientContext;
            _clientContext.State = RtmpClientState.HandshakeC0;

            await OnRtmpClientCreatedAsync();
        }

        public async Task<bool> HandleClientLoopAsync(ReadOnlyNetworkStream networkStream, CancellationToken cancellationToken)
        {
            switch (_clientContext.State)
            {
                case RtmpClientState.HandshakeC0:
                    return await HandleHandshakeC0(_clientContext, networkStream, cancellationToken);
                case RtmpClientState.HandshakeC1:
                    return await HandleHandshakeC1(_clientContext, networkStream, cancellationToken);
                case RtmpClientState.HandshakeC2:
                    return await HandleHandshakeC2(_clientContext, networkStream, cancellationToken);
                default:
                    return await HandleChunkAsync(_clientContext, networkStream, cancellationToken);
            }
        }

        private async Task<bool> HandleHandshakeC0(IRtmpClientContext clientContext, ReadOnlyNetworkStream networkStream, CancellationToken cancellationToken)
        {
            return await _mediator.Send(new RtmpHandshakeC0Event(clientContext, networkStream), cancellationToken);
        }

        private async Task<bool> HandleHandshakeC1(IRtmpClientContext clientContext, ReadOnlyNetworkStream networkStream, CancellationToken cancellationToken)
        {
            return await _mediator.Send(new RtmpHandshakeC1Event(clientContext, networkStream), cancellationToken);
        }

        private async Task<bool> HandleHandshakeC2(IRtmpClientContext clientContext, ReadOnlyNetworkStream networkStream, CancellationToken cancellationToken)
        {
            return await _mediator.Send(new RtmpHandshakeC2Event(clientContext, networkStream), cancellationToken);
        }

        private async Task<bool> HandleChunkAsync(IRtmpClientContext clientContext, ReadOnlyNetworkStream networkStream, CancellationToken cancellationToken)
        {
            return await _mediator.Send(new RtmpChunkEvent(clientContext, networkStream), cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await OnRtmpClientDisposedAsync();
        }

        private async Task OnRtmpClientCreatedAsync()
        {
            await _eventDispatcher.RtmpClientCreatedAsync(_clientContext);
        }

        private async Task OnRtmpClientDisposedAsync()
        {
            await _eventDispatcher.RtmpClientDisposedAsync(_clientContext);
        }
    }
}