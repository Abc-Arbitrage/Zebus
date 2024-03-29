﻿using System;
using System.Linq;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Lotus;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Testing.Transport;
using Abc.Zebus.Tests.Messages;
using Abc.Zebus.Util;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Core
{
    public partial class BusTests
    {
        public class DispatchErrors : BusTests
        {
            public override void Setup()
            {
                base.Setup();

                _configuration.IsErrorPublicationEnabled = true;
            }

            [Test]
            public void should_send_a_MessageProcessingFailed_on_command_error()
            {
                SetupPeersHandlingMessage<MessageProcessingFailed>(_peerUp);

                _bus.Start();

                using (SystemDateTime.PauseTime())
                using (MessageId.PauseIdGeneration())
                {
                    var command = new FakeCommand(123);
                    var exception = new Exception("Exception message");
                    SetupDispatch(command, error: exception);

                    var transportMessageReceived = command.ToTransportMessage(_peerUp);
                    _transport.RaiseMessageReceived(transportMessageReceived);

                    var commandJson = JsonConvert.SerializeObject(command);
                    var expectedTransportMessage = new MessageProcessingFailed(transportMessageReceived, commandJson, exception.ToString(), SystemDateTime.UtcNow, new[] { typeof(FakeMessageHandler).FullName }).ToTransportMessage(_self);
                    _transport.Expect(new TransportMessageSent(expectedTransportMessage, _peerUp));
                }
            }

            [Test]
            public void should_send_a_MessageProcessingFailed_on_dispatch_error()
            {
                SetupPeersHandlingMessage<MessageProcessingFailed>(_peerUp);

                _bus.Start();

                using (SystemDateTime.PauseTime())
                using (MessageId.PauseIdGeneration())
                {
                    var command = new FakeCommand(123);
                    var transportMessageReceived = command.ToTransportMessage(_peerUp);
                    var dispatch = _bus.CreateMessageDispatch(transportMessageReceived);
                    var exception = new Exception("Test error");

                    dispatch.SetHandlerCount(1);
                    var invokerMock = new Mock<IMessageHandlerInvoker>();
                    invokerMock.SetupGet(x => x.MessageHandlerType).Returns(typeof(FakeMessageHandler));
                    dispatch.SetHandled(invokerMock.Object, exception);

                    var commandJson = JsonConvert.SerializeObject(command);
                    var expectedTransportMessage = new MessageProcessingFailed(transportMessageReceived, commandJson, exception.ToString(), SystemDateTime.UtcNow, new[] { typeof(FakeMessageHandler).FullName }).ToTransportMessage(_self);
                    _transport.Expect(new TransportMessageSent(expectedTransportMessage, _peerUp));
                }
            }

            [Test]
            public void should_send_a_MessageProcessingFailed_on_event_error()
            {
                SetupPeersHandlingMessage<MessageProcessingFailed>(_peerUp);

                _bus.Start();

                using (SystemDateTime.PauseTime())
                using (MessageId.PauseIdGeneration())
                {
                    var message = new FakeEvent(123);
                    var exception = new Exception("Exception message");
                    SetupDispatch(message, error: exception);

                    var transportMessageReceived = message.ToTransportMessage(_peerUp);
                    _transport.RaiseMessageReceived(transportMessageReceived);

                    var messageJson = JsonConvert.SerializeObject(message);
                    var expectedTransportMessage = new MessageProcessingFailed(transportMessageReceived, messageJson, exception.ToString(), SystemDateTime.UtcNow, new[] { typeof(FakeMessageHandler).FullName }).ToTransportMessage(_self);
                    _transport.Expect(new TransportMessageSent(expectedTransportMessage, _peerUp));
                }
            }

            [Test]
            public void should_not_send_a_MessageProcessingFailed_when_error_publication_is_not_enabled()
            {
                _configuration.IsErrorPublicationEnabled = false;

                SetupPeersHandlingMessage<MessageProcessingFailed>(_peerUp);

                _bus.Start();

                using (SystemDateTime.PauseTime())
                using (MessageId.PauseIdGeneration())
                {
                    var message = new FakeEvent(123);
                    var exception = new Exception("Exception message");
                    SetupDispatch(message, error: exception);

                    _transport.RaiseMessageReceived(message.ToTransportMessage(_peerUp));

                    _transport.ExpectNothing();
                }
            }

            [Test]
            public void should_send_a_MessageProcessingFailed_on_error_with_local_processing()
            {
                SetupPeersHandlingMessage<MessageProcessingFailed>(_peerUp);

                _bus.Start();

                using (SystemDateTime.PauseTime())
                using (MessageId.PauseIdGeneration())
                {
                    var command = new FakeCommand(123);
                    var exception = new Exception("Exception message");
                    SetupDispatch(command, error: exception);
                    SetupPeersHandlingMessage<FakeCommand>(_self);

                    _bus.Send(command);

                    var commandJson = JsonConvert.SerializeObject(command);
                    var expectedTransportMessage = new MessageProcessingFailed(command.ToTransportMessage(_self), commandJson, exception.ToString(), SystemDateTime.UtcNow, new[] { typeof(FakeMessageHandler).FullName }).ToTransportMessage(_self);
                    _transport.Expect(new TransportMessageSent(expectedTransportMessage, _peerUp));
                }
            }

            [Test]
            public void should_send_a_CustomProcessingFailed_on_error_with_local_processing_and_unserializable_message()
            {
                SetupPeersHandlingMessage<CustomProcessingFailed>(_peerUp);

                _bus.Start();

                using (SystemDateTime.PauseTime())
                using (MessageId.PauseIdGeneration())
                {
                    SetupPeersHandlingMessage<UnserializableMessage>(_self);

                    var command = new UnserializableMessage();
                    SetupDispatch(command, error: new Exception("Dispatch exception"));

                    _bus.Send(command);

                    var error = _transport.MessagesSent.OfType<CustomProcessingFailed>().ExpectedSingle();
                    error.ExceptionMessage.ShouldContain("Dispatch exception");
                    error.ExceptionMessage.ShouldContain("Unable to serialize message");
                    error.ExceptionMessage.ShouldContain(command.GetType().FullName);
                }
            }

            [Test]
            public void should_not_send_a_MessageProcessingFailed_on_MessageProcessingException_with_local_processing()
            {
                SetupPeersHandlingMessage<MessageProcessingFailed>(_peerUp);

                _bus.Start();

                using (SystemDateTime.PauseTime())
                using (MessageId.PauseIdGeneration())
                {
                    var command = new FakeCommand(123);
                    SetupDispatch(command, error: new MessageProcessingException("Exception message") { ErrorCode = 123 });
                    SetupPeersHandlingMessage<FakeCommand>(_self);

                    _bus.Send(command);

                    _transport.ExpectNothing();
                }
            }

            [Test]
            public void should_send_a_MessageProcessingFailed_on_MessageProcessingException_with_error_publication_with_local_processing()
            {
                SetupPeersHandlingMessage<MessageProcessingFailed>(_peerUp);

                _bus.Start();

                using (SystemDateTime.PauseTime())
                using (MessageId.PauseIdGeneration())
                {
                    var command = new FakeCommand(123);
                    var exception = new MessageProcessingException("Exception message") { ErrorCode = 123, ShouldPublishError = true };
                    SetupDispatch(command, error: exception);
                    SetupPeersHandlingMessage<FakeCommand>(_self);

                    _bus.Send(command);

                    var commandJson = JsonConvert.SerializeObject(command);
                    var expectedTransportMessage = new MessageProcessingFailed(command.ToTransportMessage(_self), commandJson, exception.ToString(), SystemDateTime.UtcNow, new[] { typeof(FakeMessageHandler).FullName }).ToTransportMessage(_self);
                    _transport.Expect(new TransportMessageSent(expectedTransportMessage, _peerUp));
                }
            }
        }
    }
}
