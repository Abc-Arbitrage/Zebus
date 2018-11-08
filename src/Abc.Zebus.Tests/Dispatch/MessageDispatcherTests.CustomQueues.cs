using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Dispatch.DispatchMessages;
using Abc.Zebus.Tests.Dispatch.DispatchMessages.Namespace1;
using Abc.Zebus.Tests.Dispatch.DispatchMessages.Namespace1.Namespace2;
using Abc.Zebus.Tests.Messages;
using Abc.Zebus.Util;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Dispatch
{
    public partial class MessageDispatcherTests
    {
        [Test]
        public void should_dispatch_message_to_queue_name()
        {
            _messageDispatcher.LoadMessageHandlerInvokers();

            var syncHandler = new SyncCommandHandler();
            _containerMock.Setup(x => x.GetInstance(typeof(SyncCommandHandler))).Returns(syncHandler);

            var handler1List = new List<SyncCommandHandlerWithQueueName1>();
            _containerMock.Setup(x => x.GetInstance(typeof(SyncCommandHandlerWithQueueName1))).Returns(() =>
            {
                var handler1 = new SyncCommandHandlerWithQueueName1 { WaitForSignal = true };
                handler1List.Add(handler1);
                return handler1;
            });

            var handler2List = new List<SyncCommandHandlerWithQueueName2>();
            _containerMock.Setup(x => x.GetInstance(typeof(SyncCommandHandlerWithQueueName2))).Returns(() =>
            {
                var handler2 = new SyncCommandHandlerWithQueueName2 { WaitForSignal = true };
                handler2List.Add(handler2);
                return handler2;
            });

            DispatchFromDefaultDispatchQueue(new DispatchCommand());

            syncHandler.Called.ShouldBeTrue("Sync handler should be run synchronously");

            Wait.Until(() => handler1List.Count == 1 && handler1List[0].HandleStarted, 5.Seconds(), "First handler should be started");
            Wait.Until(() => handler2List.Count == 1 && handler2List[0].HandleStarted, 5.Seconds(), "second handler should be started");

            syncHandler.Called = false;
            DispatchFromDefaultDispatchQueue(new DispatchCommand());

            syncHandler.Called.ShouldBeTrue("Sync handler should be run synchronously");
            handler1List.Count.ShouldEqual(1, "Next handler should not be created yet");
            handler2List.Count.ShouldEqual(1, "Next handler should not be created yet");

            handler1List[0].CalledSignal.Set();
            Wait.Until(() => handler1List[0].HandleStopped, 5.Seconds(), "First handler should be stopped");
            Wait.Until(() => handler1List.Count == 2, 5.Seconds(), "Next handler should be created");
            Wait.Until(() => handler1List[1].HandleStarted, 5.Seconds(), "Next handler should be started");

            handler1List[1].CalledSignal.Set();
            Wait.Until(() => handler1List[1].HandleStopped, 5.Seconds(), "Next handler should be stopped");

            handler2List[0].CalledSignal.Set();
            Wait.Until(() => handler2List[0].HandleStopped, 5.Seconds(), "First handler should be stopped");
            Wait.Until(() => handler2List.Count == 2, 5.Seconds(), "Next handler should be created");
            handler2List[1].CalledSignal.Set();
            Wait.Until(() => handler2List[0].HandleStopped && handler2List[1].HandleStopped, 5.Seconds(), "Both handlers should be run");
        }

        [Test]
        public void should_set_queue_name_in_message_context()
        {
            _messageDispatcher.LoadMessageHandlerInvokers();

            var handler1 = new SyncCommandHandlerWithQueueName1();
            _containerMock.Setup(x => x.GetInstance(typeof(SyncCommandHandlerWithQueueName1))).Returns(handler1);

            Dispatch(new DispatchCommand());

            Wait.Until(() => handler1.HandleStopped, 5.Seconds());

            handler1.DispatchQueueName.ShouldEqual("DispatchQueue1");
        }

        [Test]
        public void should_run_invoker_synchronously_if_dispatch_queue_name_equals_current_queue_name()
        {
            _messageDispatcher.LoadMessageHandlerInvokers();

            var handler1 = new SyncCommandHandlerWithQueueName1
            {
                Callback = () =>
                {
                    var handler2 = new SyncCommandHandlerWithQueueName1();
                    _containerMock.Setup(x => x.GetInstance(typeof(SyncCommandHandlerWithQueueName1))).Returns(handler2);

                    // should be run synchronously
                    Dispatch(new DispatchCommand());

                    Wait.Until(() => handler2.HandleStopped, 5.Seconds());
                }
            };
            _containerMock.Setup(x => x.GetInstance(typeof(SyncCommandHandlerWithQueueName1))).Returns(handler1);

            // should be enqueued
            Dispatch(new DispatchCommand());

            Wait.Until(() => handler1.HandleStopped, 5.Seconds());
        }

        [Test]
        public void should_not_hang_when_running_invoker_synchronously_in_same_dispatch_queue()
        {
            _messageDispatcher.ConfigureHandlerFilter(x => x == typeof(ForwardCommandHandler) || x == typeof(SyncCommandHandlerWithQueueName1));
            _messageDispatcher.LoadMessageHandlerInvokers();

            var handler1 = new ForwardCommandHandler { Action = x => Dispatch(new DispatchCommand()) };
            _containerMock.Setup(x => x.GetInstance(typeof(ForwardCommandHandler))).Returns(handler1);

            var handler2 = new SyncCommandHandlerWithQueueName1();
            _containerMock.Setup(x => x.GetInstance(typeof(SyncCommandHandlerWithQueueName1))).Returns(handler2);

            Dispatch(new ForwardCommand());

            Wait.Until(() => handler2.HandleStopped, 5.Seconds());
        }

        [Test]
        public void should_use_queue_name_from_namespace()
        {
            _messageDispatcher.LoadMessageHandlerInvokers();

            var handler = new SyncCommandHandlerWithOtherQueueName();
            _containerMock.Setup(x => x.GetInstance(typeof(SyncCommandHandlerWithOtherQueueName))).Returns(handler);

            Dispatch(new DispatchCommand());

            Wait.Until(() => handler.DispatchQueueName != null, 5.Seconds());

            var queueSource = new UseOtherQueue();
            handler.DispatchQueueName.ShouldEqual(queueSource.QueueName);
        }

        [Test]
        public void should_wait_for_dispatch_to_stop()
        {
            _messageDispatcher.LoadMessageHandlerInvokers();

            var message = new ExecutableEvent { IsBlocking = true };

            Dispatch(message);

            message.HandleStarted.Wait(5.Seconds()).ShouldBeTrue();

            var stopTask = Task.Run(() => _messageDispatcher.Stop()).WaitForActivation();

            Thread.Sleep(200);
            stopTask.IsCompleted.ShouldBeFalse();

            message.Unblock();
            stopTask.Wait(5.Seconds()).ShouldBeTrue();
        }

        [Test]
        public void should_throw_exception_if_calling_dispatched_when_stopped()
        {
            _messageDispatcher.Stop();
            Assert.Throws<InvalidOperationException>(() => Dispatch(new DispatchCommand()));
        }

        [Test]
        public void should_handle_local_dispatch_when_stopping()
        {
            _messageDispatcher.LoadMessageHandlerInvokers();

            var handler1 = new SyncCommandHandlerWithQueueName1
            {
                WaitForSignal = true
            };

            var handler2 = new SyncCommandHandlerWithQueueName2
            {
                WaitForSignal = false
            };

            _containerMock.Setup(x => x.GetInstance(typeof(SyncCommandHandlerWithQueueName1))).Returns(handler1);
            _containerMock.Setup(x => x.GetInstance(typeof(SyncCommandHandlerWithQueueName2))).Returns(handler2);

            Dispatch(new DispatchCommand());

            Wait.Until(() => handler1.HandleStarted, 10.Seconds());
            var stopTask = Task.Run(() => _messageDispatcher.Stop()).WaitForActivation();

            Wait.Until(() => _messageDispatcher.Status == MessageDispatcherStatus.Stopping, 10.Seconds());
            Wait.Until(() => handler2.HandleStopped, 10.Seconds());

            handler1.WaitForSignal = false;
            handler2.HandleStopped = false;

            Dispatch(new DispatchCommand(), true);

            Wait.Until(() => handler2.HandleStopped, 10.Seconds());
            handler1.CalledSignal.Set();

            stopTask.Wait(10.Seconds()).ShouldBeTrue();
        }

        [Test]
        public void should_not_accept_remote_dispatch_when_stopping()
        {
            _messageDispatcher.LoadMessageHandlerInvokers();

            var handler1 = new SyncCommandHandlerWithQueueName1
            {
                WaitForSignal = true
            };

            var handler2 = new SyncCommandHandlerWithQueueName2
            {
                WaitForSignal = false
            };

            _containerMock.Setup(x => x.GetInstance(typeof(SyncCommandHandlerWithQueueName1))).Returns(handler1);
            _containerMock.Setup(x => x.GetInstance(typeof(SyncCommandHandlerWithQueueName2))).Returns(handler2);

            Dispatch(new DispatchCommand());

            Wait.Until(() => handler1.HandleStarted, 5.Seconds());
            var stopTask = Task.Run(() => _messageDispatcher.Stop()).WaitForActivation();

            Wait.Until(() => _messageDispatcher.Status == MessageDispatcherStatus.Stopping, 10.Seconds());
            Wait.Until(() => handler2.HandleStopped, 5.Seconds());

            handler1.WaitForSignal = false;
            handler2.HandleStopped = false;

            Assert.Throws<InvalidOperationException>(() => Dispatch(new DispatchCommand()));

            handler1.CalledSignal.Set();
            stopTask.Wait(5.Seconds()).ShouldBeTrue();
        }

        [Test, Repeat(5)]
        public void should_restart_dispatch_queues()
        {
            _messageDispatcher.LoadMessageHandlerInvokers();

            Dispatch(new DispatchCommand());

            _dispatchQueueFactory.DispatchQueues.Count.ShouldBeGreaterOrEqualThan(1);

            _messageDispatcher.Stop();

            foreach (var dispatchQueue in _dispatchQueueFactory.DispatchQueues)
            {
                dispatchQueue.IsRunning.ShouldBeFalse();
            }

            _messageDispatcher.Start();

            foreach (var dispatchQueue in _dispatchQueueFactory.DispatchQueues)
            {
                dispatchQueue.IsRunning.ShouldBeTrue();
            }
        }

        [Test]
        public void should_clone_message_when_dispatching_locally_to_a_different_queue()
        {
            _messageDispatcher.LoadMessageHandlerInvokers();

            var handler = new SyncCommandHandlerWithQueueName1();
            _containerMock.Setup(x => x.GetInstance(typeof(SyncCommandHandlerWithQueueName1))).Returns(handler);

            var command = new DispatchCommand();
            Dispatch(command, true);

            Wait.Until(() => handler.HandleStopped, 5.Seconds());

            handler.ReceivedMessage.ShouldNotBeNull();
            handler.ReceivedMessage.ShouldNotBeTheSameAs(command);
            handler.ReceivedMessage.Guid.ShouldEqual(command.Guid);
        }

        [Test]
        public void should_not_clone_message_when_dispatching_locally_to_the_current_queue()
        {
            _messageDispatcher.LoadMessageHandlerInvokers();

            var handler = new SyncCommandHandler();
            _containerMock.Setup(x => x.GetInstance(typeof(SyncCommandHandler))).Returns(handler);

            var command = new DispatchCommand();
            DispatchFromDefaultDispatchQueue(command);

            Wait.Until(() => handler.Called, 5.Seconds());

            handler.ReceivedMessage.ShouldNotBeNull();
            handler.ReceivedMessage.ShouldBeTheSameAs(command);
        }
    }
}
