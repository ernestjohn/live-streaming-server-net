﻿using AutoFixture;
using FluentAssertions;
using LiveStreamingServerNet.Rtmp.Internal;
using LiveStreamingServerNet.Rtmp.Internal.Contracts;
using LiveStreamingServerNet.Rtmp.Internal.Extensions;
using LiveStreamingServerNet.Rtmp.Server.Auth;
using LiveStreamingServerNet.Rtmp.Server.Internal.Authorization.Contracts;
using LiveStreamingServerNet.Rtmp.Server.Internal.Contracts;
using LiveStreamingServerNet.Rtmp.Server.Internal.RtmpEventHandlers.Commands;
using LiveStreamingServerNet.Rtmp.Server.Internal.Services.Contracts;
using LiveStreamingServerNet.Rtmp.Test.Utilities;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace LiveStreamingServerNet.Rtmp.Server.Test.RtmpEventHandlers.Commands
{
    public class RtmpPublishCommandHandlerTest
    {
        private readonly IFixture _fixture;
        private readonly IRtmpChunkStreamContext _chunkStreamContext;
        private readonly IRtmpClientSessionContext _clientContext;
        private readonly IRtmpStreamManagerService _streamManager;
        private readonly IRtmpCommandMessageSenderService _commandMessageSender;
        private readonly IRtmpServerStreamEventDispatcher _eventDispatcher;
        private readonly IStreamAuthorization _streamAuthorization;
        private readonly IRtmpStream _stream;
        private readonly IRtmpPublishStreamContext _publishStreamContext;
        private readonly ILogger<RtmpPublishCommandHandler> _logger;
        private readonly RtmpPublishCommandHandler _sut;

        public RtmpPublishCommandHandlerTest()
        {
            _fixture = new Fixture();
            _chunkStreamContext = Substitute.For<IRtmpChunkStreamContext>();
            _clientContext = Substitute.For<IRtmpClientSessionContext>();
            _streamManager = Substitute.For<IRtmpStreamManagerService>();
            _commandMessageSender = Substitute.For<IRtmpCommandMessageSenderService>();
            _eventDispatcher = Substitute.For<IRtmpServerStreamEventDispatcher>();
            _streamAuthorization = Substitute.For<IStreamAuthorization>();
            _stream = Substitute.For<IRtmpStream>();
            _publishStreamContext = Substitute.For<IRtmpPublishStreamContext>();
            _logger = Substitute.For<ILogger<RtmpPublishCommandHandler>>();

            _stream.ClientContext.Returns(_clientContext);
            _publishStreamContext.Stream.Returns(_stream);

            _commandMessageSender.When(x =>
                x.SendCommandMessage(Arg.Any<IRtmpClientSessionContext>(), Arg.Any<uint>(), Arg.Any<uint>(), Arg.Any<string>(), Arg.Any<double>(),
                    Arg.Any<IReadOnlyDictionary<string, object>>(), Arg.Any<IReadOnlyList<object?>>(),
                    Arg.Any<AmfEncodingType>(), Arg.Any<Action<bool>>()))
                .Do(x => x.Arg<Action<bool>>()?.Invoke(true));

            _sut = new RtmpPublishCommandHandler(
                _streamManager,
                _commandMessageSender,
                _eventDispatcher,
                _streamAuthorization,
                _logger
            );
        }

        [Fact]
        public async Task HandleAsync_Should_ReturnFalse_When_StreamIsNotYetCreated()
        {
            // Arrange
            var transactionId = 0.0;
            var commandObject = new Dictionary<string, object>();
            var streamName = "streamName?password=123456";
            var publishingType = "live";
            var command = new RtmpPublishCommand(transactionId, commandObject, streamName, publishingType);

            _clientContext.GetStream(Arg.Any<uint>()).Returns((IRtmpStream?)null);

            // Act
            var result = await _sut.HandleAsync(_chunkStreamContext, _clientContext, command, default);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task HandleAsync_Should_ReturnFalse_If_NotAuthorized()
        {
            // Arrange
            var transactionId = 0.0;
            var commandObject = new Dictionary<string, object>();
            var streamName = "streamName?password=123456";
            var streamId = _fixture.Create<uint>();
            var publishingType = "live";
            var command = new RtmpPublishCommand(transactionId, commandObject, streamName, publishingType);

            var streamPath = "/appName/streamName";

            _chunkStreamContext.MessageHeader.MessageStreamId.Returns(streamId);
            _clientContext.GetStream(streamId).Returns(_stream);
            _clientContext.AppName.Returns("appName");
            _stream.Id.Returns(streamId);

            _streamAuthorization.AuthorizePublishingAsync(_clientContext, streamPath, publishingType, Helpers.CreateExpectedStreamArguments("password", "123456"))
                .Returns(AuthorizationResult.Unauthorized("testing"));

            // Act
            var result = await _sut.HandleAsync(_chunkStreamContext, _clientContext, command, default);

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task HandleAsync_Should_SendPlayStartAndCaches_If_AuthorizedAndStreamIsPublishedSuccessfully(bool publishStreamExists, bool gopCacheActivated)
        {
            // Arrange
            var transactionId = 0.0;
            var commandObject = new Dictionary<string, object>();
            var appName = "appName";
            var streamName = "streamName?password=123456";
            var streamPath = "/appName/streamName";
            var streamId = _fixture.Create<uint>();
            var publishingType = "live";
            var chunkStreamId = _fixture.Create<uint>();
            var timestamp = _fixture.Create<uint>();
            var command = new RtmpPublishCommand(transactionId, commandObject, streamName, publishingType);

            _clientContext.GetStream(streamId).Returns(_stream);
            _clientContext.AppName.Returns(appName);
            _chunkStreamContext.ChunkStreamId.Returns(chunkStreamId);
            _chunkStreamContext.MessageHeader.Timestamp.Returns(timestamp);
            _chunkStreamContext.MessageHeader.MessageStreamId.Returns(streamId);
            _stream.Id.Returns(streamId);

            if (publishStreamExists)
            {
                _publishStreamContext.GroupOfPicturesCacheActivated.Returns(gopCacheActivated);
                _streamManager.GetPublishStreamContext(streamPath).Returns(_publishStreamContext);
            }

            _streamAuthorization.AuthorizePublishingAsync(
                _clientContext, streamPath, publishingType, Helpers.CreateExpectedStreamArguments("password", "123456"))
                .Returns(AuthorizationResult.Authorized())
                .AndDoes(x =>
                {
                    _stream.PublishContext.Returns(_publishStreamContext);
                    _publishStreamContext.StreamPath.Returns(x.ArgAt<string>(1));
                    _publishStreamContext.StreamArguments.Returns(x.Arg<IReadOnlyDictionary<string, string>>());
                });

            _streamManager.StartPublishing(_stream, streamPath,
                Helpers.CreateExpectedStreamArguments("password", "123456"), out Arg.Any<IList<IRtmpSubscribeStreamContext>>())
                .Returns(PublishingStreamResult.Succeeded);

            // Act
            var result = await _sut.HandleAsync(_chunkStreamContext, _clientContext, command, default);

            // Assert
            Received.InOrder(() =>
            {
                _commandMessageSender.Received(1).SendCommandMessage(
                    _clientContext, streamId, RtmpConstants.OnStatusChunkStreamId, "onStatus", 0, null,
                    Helpers.CreateExpectedCommandProperties(RtmpArgumentValues.Status, RtmpStatusCodes.PublishStart),
                    Arg.Any<AmfEncodingType>(), Arg.Any<Action<bool>>());

                _ = _eventDispatcher.Received(1).RtmpStreamPublishedAsync(
                    _clientContext, streamPath, Helpers.CreateExpectedStreamArguments("password", "123456"));
            });

            result.Should().BeTrue();
        }

        [Theory]
        [InlineData(PublishingStreamResult.AlreadyPublishing)]
        [InlineData(PublishingStreamResult.AlreadySubscribing)]
        internal async Task HandleAsync_Should_SendError_If_AuthorizedButStreamIsNotPublishedSuccessfully(PublishingStreamResult publishingResult)
        {
            // Arrange
            var transactionId = 0.0;
            var commandObject = new Dictionary<string, object>();
            var appName = "appName";
            var streamName = "streamName?password=123456";
            var streamPath = "/appName/streamName";
            var streamId = _fixture.Create<uint>();
            var publishingType = "live";
            var chunkStreamId = _fixture.Create<uint>();
            var timestamp = _fixture.Create<uint>();
            var command = new RtmpPublishCommand(transactionId, commandObject, streamName, publishingType);

            _clientContext.GetStream(streamId).Returns(_stream);
            _clientContext.AppName.Returns(appName);
            _chunkStreamContext.ChunkStreamId.Returns(chunkStreamId);
            _chunkStreamContext.MessageHeader.MessageStreamId.Returns(streamId);
            _stream.Id.Returns(streamId);

            _streamAuthorization.AuthorizePublishingAsync(
                _clientContext, streamPath, publishingType, Helpers.CreateExpectedStreamArguments("password", "123456"))
                .Returns(AuthorizationResult.Authorized())
                .AndDoes(x =>
                {
                    _stream.PublishContext.Returns(_publishStreamContext);
                    _publishStreamContext.StreamPath.Returns(x.ArgAt<string>(1));
                    _publishStreamContext.StreamArguments.Returns(x.Arg<IReadOnlyDictionary<string, string>>());
                });

            _streamManager.StartPublishing(_stream, streamPath,
                Helpers.CreateExpectedStreamArguments("password", "123456"), out Arg.Any<IList<IRtmpSubscribeStreamContext>>())
                .Returns(publishingResult);

            // Act
            var result = await _sut.HandleAsync(_chunkStreamContext, _clientContext, command, default);

            // Assert
            _commandMessageSender.Received(1).SendCommandMessage(
                _clientContext, streamId, RtmpConstants.OnStatusChunkStreamId, "onStatus", 0, null,
                Helpers.CreateExpectedCommandProperties(RtmpArgumentValues.Error, RtmpStatusCodes.PublishBadConnection),
                Arg.Any<AmfEncodingType>(), Arg.Any<Action<bool>>());

            result.Should().BeTrue();
        }
    }
}
