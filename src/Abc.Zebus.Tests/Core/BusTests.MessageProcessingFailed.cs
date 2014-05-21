using System;
using Abc.Zebus.Lotus;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Transport;
using Abc.Zebus.Tests.Messages;
using Abc.Zebus.Util;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Core
{
    public partial class BusTests
    {
        [Test]
        public void should_send_a_MessageProcessingFailed_on_unknown_command_error()
        {
            using (SystemDateTime.PauseTime())
            using (MessageId.PauseIdGeneration())
            {
                var command = new FakeCommand(123);
                var commandJson = JsonConvert.SerializeObject(command);
                var exception = new Exception("Exception message");
                SetupDispatch(command, error: exception);
                var transportMessageReceived = command.ToTransportMessage(_peerUp);

                _transport.RaiseMessageReceived(transportMessageReceived);

                var expectedTransportMessage = new MessageProcessingFailed(transportMessageReceived, commandJson, exception.ToString(), SystemDateTime.UtcNow, new [] { typeof(FakeMessageHandler).FullName }).ToTransportMessage(_self);
                _transport.Expect(new TransportMessageSent(expectedTransportMessage, _peerUp));
            }
        }

        [Test]
        public void should_send_a_MessageProcessingFailed_on_unknown_event_error()
        {
            using (SystemDateTime.PauseTime())
            using (MessageId.PauseIdGeneration())
            {
                var message = new FakeEvent(123);
                var messageJson = JsonConvert.SerializeObject(message);
                var exception = new Exception("Exception message");
                SetupDispatch(message, error: exception);
                var transportMessageReceived = message.ToTransportMessage(_peerUp);

                _transport.RaiseMessageReceived(transportMessageReceived);

                var expectedTransportMessage = new MessageProcessingFailed(transportMessageReceived, messageJson, exception.ToString(), SystemDateTime.UtcNow, new[] { typeof(FakeMessageHandler).FullName }).ToTransportMessage(_self);
                _transport.Expect(new TransportMessageSent(expectedTransportMessage, _peerUp));
            }
        }

        [Test]
        public void should_send_a_MessageProcessingFailed_on_unknown_error_with_local_processing()
        {
            using (SystemDateTime.PauseTime())
            using (MessageId.PauseIdGeneration())
            {
                var command = new FakeCommand(123);
                var commandJson = JsonConvert.SerializeObject(command);
                var exception = new Exception("Exception message");
                SetupDispatch(command, error: exception);
                SetupPeersHandlingMessage<FakeCommand>(_self);

                _bus.Send(command);

                var expectedTransportMessage = new MessageProcessingFailed(command.ToTransportMessage(_self), commandJson, exception.ToString(), SystemDateTime.UtcNow, new[] { typeof(FakeMessageHandler).FullName }).ToTransportMessage(_self);
                _transport.Expect(new TransportMessageSent(expectedTransportMessage, _peerUp));
            }
        }


        [Test]
        public void should_not_send_a_MessageProcessingFailed_on_domain_exception_with_local_processing()
        {
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