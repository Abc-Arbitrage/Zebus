using System;
using System.Linq;
using Abc.Zebus.Lotus;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Testing.Transport;
using Abc.Zebus.Tests.Messages;
using Abc.Zebus.Util;
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

                _configuration.SetupGet(x => x.IsErrorPublicationEnabled).Returns(true);
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
                _configuration.SetupGet(x => x.IsErrorPublicationEnabled).Returns(false);

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
                    SetupPeersHandlingMessage<FakeCommand>(_self);

                    var command = new FakeCommand(123);
                    SetupDispatch(command, error: new Exception("Dispatch exception"));

                    _messageSerializer.AddSerializationExceptionFor(command.TypeId(), exceptionMessage: "Serialization exception");

                    _bus.Send(command);

                    var error = _transport.MessagesSent.OfType<CustomProcessingFailed>().ExpectedSingle();
                    error.ExceptionMessage.ShouldContain("Dispatch exception");
                    error.ExceptionMessage.ShouldContain("Serialization exception");
                    error.ExceptionMessage.ShouldContain(command.GetType().FullName);
                }
            }

            [Test]
            public void should_not_send_a_MessageProcessingFailed_on_domain_exception_with_local_processing()
            {
                SetupPeersHandlingMessage<MessageProcessingFailed>(_peerUp);

                _bus.Start();

                using (SystemDateTime.PauseTime())
                using (MessageId.PauseIdGeneration())
                {
                    var command = new FakeCommand(123);
                    SetupDispatch(command, error: new DomainException(123, "Exception message"));
                    SetupPeersHandlingMessage<FakeCommand>(_self);

                    _bus.Send(command);

                    _transport.ExpectNothing();
                }
            }
        }
    }
}