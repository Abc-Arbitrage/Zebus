using System.Linq;
using Abc.Zebus.Core;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Messages;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Core
{
    public partial class BusTests
    {
        public class Replay : BusTests
        {
            [Test]
            public void handlers_reply_with_an_int()
            {
                using (MessageId.PauseIdGeneration())
                {
                    const int commandReply = 456;
                    var command = new FakeCommand(123);
                    SetupDispatch(command, _ => _bus.Reply(commandReply));

                    var transportMessageReceived = command.ToTransportMessage(_peerUp);
                    var expectedTransportMessage = new MessageExecutionCompleted(transportMessageReceived.Id, commandReply, null).ToTransportMessage(_self);

                    _transport.RaiseMessageReceived(transportMessageReceived);

                    var sentMessage = _transport.Messages.Single();
                    expectedTransportMessage.ShouldHaveSamePropertiesAs(sentMessage.TransportMessage);
                    var destination = sentMessage.Targets.Single();
                    destination.ShouldHaveSamePropertiesAs(_peerUp);
                }
            }

            [Test]
            public void handlers_reply_with_an_int_and_a_message()
            {
                using (MessageId.PauseIdGeneration())
                {
                    const int commandReply = 456;
                    const string replyMessage = "Test";

                    var command = new FakeCommand(123);
                    SetupDispatch(command, _ => _bus.Reply(commandReply, replyMessage));

                    var transportMessageReceived = command.ToTransportMessage(_peerUp);
                    var expectedTransportMessage = new MessageExecutionCompleted(transportMessageReceived.Id, commandReply, replyMessage).ToTransportMessage(_self);

                    _transport.RaiseMessageReceived(transportMessageReceived);

                    var sentMessage = _transport.Messages.Single();
                    expectedTransportMessage.ShouldHaveSamePropertiesAs(sentMessage.TransportMessage);
                    var destination = sentMessage.Targets.Single();
                    destination.ShouldHaveSamePropertiesAs(_peerUp);
                }
            }

            [Test]
            public void handlers_reply_with_an_object()
            {
                using (MessageId.PauseIdGeneration())
                {
                    var command = new FakeCommand(123);
                    var commandResult = new FakeCommandResult("CommandResult", 45);
                    var transportMessageReceived = command.ToTransportMessage(_peerUp);
                    SetupDispatch(command, _ => _bus.Reply(commandResult));

                    var expectedExecutionCompleted = MessageExecutionCompleted.Success(transportMessageReceived.Id, commandResult, _messageSerializer);
                    var expectedTransportMessage = expectedExecutionCompleted.ToTransportMessage(_self);

                    _transport.RaiseMessageReceived(transportMessageReceived);

                    var sentMessage = _transport.Messages.Single();
                    expectedTransportMessage.ShouldHaveSamePropertiesAs(sentMessage.TransportMessage);
                    var destination = sentMessage.Targets.Single();
                    destination.ShouldHaveSamePropertiesAs(_peerUp);
                }
            }
        }
    }
}