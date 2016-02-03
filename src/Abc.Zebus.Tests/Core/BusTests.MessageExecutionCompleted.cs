using System;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Core;
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
        [Test]
        public void shoud_not_send_a_completion_message_for_events()
        {
            using (MessageId.PauseIdGeneration())
            {
                var @event = new FakeEvent(123);

                var transportMessageReceived = @event.ToTransportMessage(_peerUp);
                _transport.RaiseMessageReceived(transportMessageReceived);

                _transport.Messages.ShouldNotContain(x => x.TransportMessage.MessageTypeId == MessageExecutionCompleted.TypeId);
            }
        }

        [Test]
        public void shoud_send_a_completion_message_without_error_code()
        {
            using (MessageId.PauseIdGeneration())
            {
                var command = new FakeCommand(123);
                SetupDispatch(command);

                var transportMessageReceived = command.ToTransportMessage(_peerUp);
                _transport.RaiseMessageReceived(transportMessageReceived);

                var messageExecutionCompleted = new MessageExecutionCompleted(transportMessageReceived.Id, 0, null).ToTransportMessage(_self);
                _transport.ExpectExactly(new TransportMessageSent(messageExecutionCompleted, _peerUp));
            }
        }

        [Test]
        public void shoud_send_a_completion_message_with_error_code_on_exception()
        {
            using (MessageId.PauseIdGeneration())
            {
                var command = new FakeCommand(123);
                SetupDispatch(command, error: new Exception());
                var transportMessageReceived = command.ToTransportMessage(_peerUp);

                _transport.RaiseMessageReceived(transportMessageReceived);

                var expectedTransportMessage = new MessageExecutionCompleted(transportMessageReceived.Id, 1, null).ToTransportMessage(_self);
                _transport.Expect(new TransportMessageSent(expectedTransportMessage, _peerUp));
            }
        }

        [Test]
        public void shoud_send_a_completion_message_with_domain_exception_error_code_and_message()
        {
            using (MessageId.PauseIdGeneration())
            {
                const int domainExceptionValue = 5000;
                const string domainExceptionMessage = "Domain Exception";

                var command = new FakeCommand(123);
                SetupDispatch(command, error: new DomainException(domainExceptionValue, domainExceptionMessage));
                var transportMessageReceived = command.ToTransportMessage(_peerUp);

                _transport.RaiseMessageReceived(transportMessageReceived);

                var expectedTransportMessage = new MessageExecutionCompleted(transportMessageReceived.Id, domainExceptionValue, domainExceptionMessage).ToTransportMessage(_self);
                _transport.ExpectExactly(new TransportMessageSent(expectedTransportMessage, _peerUp));
            }
        }

        [Test]
        public void should_release_task_when_completion_message_is_sent()
        {
            using (MessageId.PauseIdGeneration())
            {
                var command = new FakeCommand(123);
                SetupPeersHandlingMessage<FakeCommand>(_peerUp);
                _bus.Start();

                var task = _bus.Send(command);
                var commandCompleted = new MessageExecutionCompleted(MessageId.NextId(), 1, "Error message");
                _transport.RaiseMessageReceived(commandCompleted.ToTransportMessage());

                var receivedAck = task.Wait(10);

                receivedAck.ShouldBeTrue();
                task.Result.ErrorCode.ShouldEqual(1);
                task.Result.ResponseMessage.ShouldEqual("Error message");
            }
        }

        [Test]
        public void should_not_continue_execution_after_awaiting_a_send_in_the_MessageReceived_thread()
        {
            using (MessageId.PauseIdGeneration())
            {
                var command = new FakeCommand(456);
                SetupPeersHandlingMessage<FakeCommand>(_peerUp);
                _bus.Start();

                var task = _bus.Send(command);
                var commandCompleted = new MessageExecutionCompleted(MessageId.NextId(), 0, null);
                int backgroundThreadId = 0;

                BackgroundThread.Start(() =>
                    {
                        backgroundThreadId = Thread.CurrentThread.ManagedThreadId;
                        _transport.RaiseMessageReceived(commandCompleted.ToTransportMessage());
                    });

                var getThreadIdTask = GetThreadIfAfterAwaitingCommandResult(task);

                getThreadIdTask.Result.ShouldNotEqual(backgroundThreadId);
            }
        }

        private async Task<int> GetThreadIfAfterAwaitingCommandResult(Task<CommandResult> task)
        {
            await task;
            return Thread.CurrentThread.ManagedThreadId;
        }
    }
}