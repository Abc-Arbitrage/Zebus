using System;
using System.IO;
using System.Linq;
using Abc.Zebus.Lotus;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Testing.Transport;
using Abc.Zebus.Tests.Messages;
using Abc.Zebus.Transport;
using Abc.Zebus.Util;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Core
{
    public partial class BusTests
    {
        public class DeserializationErrors : BusTests
        {
            public override void Setup()
            {
                base.Setup();

                _bus.DeserializationFailureDumpDirectoryPath = PathUtil.InBaseDirectory(Guid.NewGuid().ToString().Substring(0, 4));
                _configuration.IsErrorPublicationEnabled = true;
            }

            [Test]
            public void should_send_MessageProcessingFailed_if_unable_to_deserialize_message()
            {
                SetupPeersHandlingMessage<MessageProcessingFailed>(_peerUp);

                _bus.Start();

                var command = new FakeCommand(123);
                _messageSerializer.AddSerializationExceptionFor(command.TypeId(), "Serialization error");

                using (SystemDateTime.PauseTime())
                using (MessageId.PauseIdGeneration())
                {
                    var transportMessage = command.ToTransportMessage();

                    var messageProcessingFailedBytes = new MessageProcessingFailed(null, null, null, DateTime.UtcNow, null).ToTransportMessage().Content;
                    _messageSerializer.AddSerializationFuncFor<MessageProcessingFailed>(x =>
                    {
                        x.FailingMessage.ShouldEqual(transportMessage);
                        x.ExceptionUtcTime.ShouldEqual(SystemDateTime.UtcNow);
                        x.ExceptionMessage.ShouldContain("Unable to deserialize message");
                        return messageProcessingFailedBytes;
                    });

                    _transport.RaiseMessageReceived(transportMessage);

                    var processingFailedTransportMessage = new TransportMessage(MessageUtil.TypeId<MessageProcessingFailed>(), messageProcessingFailedBytes, _self);
                    _transport.ExpectExactly(new TransportMessageSent(processingFailedTransportMessage, _peerUp));
                }
            }

            [Test]
            public void should_send_MessageProcessingFailed_when_error_publication_os_not_enabled()
            {
                _configuration.IsErrorPublicationEnabled = false;

                SetupPeersHandlingMessage<MessageProcessingFailed>(_peerUp);

                _bus.Start();

                var exception = new Exception("Expected exception");
                _messageSerializer.AddSerializationExceptionFor<FakeCommand>(exception);

                _transport.RaiseMessageReceived(new FakeCommand(123).ToTransportMessage());

                _transport.ExpectNothing();
            }

            [Test]
            public void should_include_exception_in_MessageProcessingFailed()
            {
                SetupPeersHandlingMessage<MessageProcessingFailed>(_peerUp);

                _bus.Start();

                var exception = new Exception("Expected exception");
                _messageSerializer.AddSerializationExceptionFor<FakeCommand>(exception);

                _transport.RaiseMessageReceived(new FakeCommand(123).ToTransportMessage());

                var message = _transport.MessagesSent.OfType<MessageProcessingFailed>().ExpectedSingle();
                message.ExceptionMessage.ShouldContain(exception.ToString());
            }

            [Test]
            public void should_include_dump_path_in_MessageProcessingFailed()
            {
                SetupPeersHandlingMessage<MessageProcessingFailed>(_peerUp);

                _bus.Start();

                var exception = new Exception("Expected exception");
                _messageSerializer.AddSerializationExceptionFor<FakeCommand>(exception);

                _transport.RaiseMessageReceived(new FakeCommand(123).ToTransportMessage());

                var message = _transport.MessagesSent.OfType<MessageProcessingFailed>().ExpectedSingle();
                message.ExceptionMessage.ShouldContain(_bus.DeserializationFailureDumpDirectoryPath);
            }

            [Test]
            public void should_ack_transport_when_handling_undeserializable_message()
            {
                var command = new FakeCommand(123);
                _messageSerializer.AddSerializationExceptionFor(command.TypeId());

                var transportMessage = command.ToTransportMessage();
                _transport.RaiseMessageReceived(transportMessage);

                _transport.AckedMessages.ShouldContain(transportMessage);
            }

            [Test]
            public void should_dump_incoming_message_if_unable_to_deserialize_it()
            {
                var command = new FakeCommand(123);
                _messageSerializer.AddSerializationExceptionFor(command.TypeId());

                var transportMessage = command.ToTransportMessage();
                _transport.RaiseMessageReceived(transportMessage);

                var dumpFileName = System.IO.Directory.GetFiles(_bus.DeserializationFailureDumpDirectoryPath).ExpectedSingle();
                dumpFileName.ShouldContain("Abc.Zebus.Tests.Messages.FakeCommand");
                File.ReadAllBytes(dumpFileName).Length.ShouldEqual(2);
            }
        }
    }
}
