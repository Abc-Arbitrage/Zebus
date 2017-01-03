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
using Abc.Zebus.Util;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Dispatch
{
    public partial class MessageDispatcherTests
    {
        [Test, Timeout(3000)]
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

            Wait.Until(() => handler1List.Count == 1 && handler1List[0].HandleStarted, 150.Milliseconds(), "First handler should be started");
            Wait.Until(() => handler2List.Count == 1 && handler2List[0].HandleStarted, 150.Milliseconds(), "second handler should be started");

            syncHandler.Called = false;
            DispatchFromDefaultDispatchQueue(new DispatchCommand());

            syncHandler.Called.ShouldBeTrue("Sync handler should be run synchronously");
            handler1List.Count.ShouldEqual(1, "Next handler should not be created yet");
            handler2List.Count.ShouldEqual(1, "Next handler should not be created yet");

            handler1List[0].CalledSignal.Set();
            Wait.Until(() => handler1List[0].HandleStopped, 150.Milliseconds(), "First handler should be stopped");
            Wait.Until(() => handler1List.Count == 2, 150.Milliseconds(), "Next handler should be created");
            Wait.Until(() => handler1List[1].HandleStarted, 150.Milliseconds(), "Next handler should be started");

            handler1List[1].CalledSignal.Set();
            Wait.Until(() => handler1List[1].HandleStopped, 150.Milliseconds(), "Next handler should be stopped");

            handler2List[0].CalledSignal.Set();
            Wait.Until(() => handler2List[0].HandleStopped, 150.Milliseconds(), "First handler should be stopped");
            Wait.Until(() => handler2List.Count == 2, 150.Milliseconds(), "Next handler should be created");
            handler2List[1].CalledSignal.Set();
            Wait.Until(() => handler2List[0].HandleStopped && handler2List[1].HandleStopped, 150.Milliseconds(), "Both handlers should be run");
        }

        [Test]
        public void should_set_queue_name_in_message_context()
        {
            _messageDispatcher.LoadMessageHandlerInvokers();

            var handler1 = new SyncCommandHandlerWithQueueName1();
            _containerMock.Setup(x => x.GetInstance(typeof(SyncCommandHandlerWithQueueName1))).Returns(handler1);

            Dispatch(new DispatchCommand());

            Wait.Until(() => handler1.HandleStopped, 150.Milliseconds());

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

                    Wait.Until(() => handler2.HandleStopped, 500.Milliseconds());
                }
            };
            _containerMock.Setup(x => x.GetInstance(typeof(SyncCommandHandlerWithQueueName1))).Returns(handler1);

            // should be enqueued
            Dispatch(new DispatchCommand());

            Wait.Until(() => handler1.HandleStopped, 500.Milliseconds());
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

            Wait.Until(() => handler2.HandleStopped, 1000.Milliseconds());
        }

        [Test]
        public void should_use_queue_name_from_namespace()
        {
            _messageDispatcher.LoadMessageHandlerInvokers();

            var handler = new SyncCommandHandlerWithOtherQueueName();
            _containerMock.Setup(x => x.GetInstance(typeof(SyncCommandHandlerWithOtherQueueName))).Returns(handler);

            Dispatch(new DispatchCommand());

            Wait.Until(() => handler.DispatchQueueName != null, 500.Milliseconds());

            var queueSource = new UseOtherQueue();
            handler.DispatchQueueName.ShouldEqual(queueSource.QueueName);
        }

        [Test]
        public void should_wait_for_TaskSchedulers_to_stop()
        {
            _messageDispatcher.LoadMessageHandlerInvokers();

            var command = new BlockableCommand { IsBlocking = true };

            Dispatch(command);

            Wait.Until(() => command.HandleStarted, 500.Milliseconds());

            var stopTask = Task.Run(() => _messageDispatcher.Stop());

            Thread.Sleep(200);
            stopTask.IsCompleted.ShouldBeFalse();

            command.BlockingSignal.Set();
            stopTask.Wait(500.Milliseconds()).ShouldBeTrue();
        }

        [Test]
        public void should_throw_exception_if_calling_dispatched_when_stopped()
        {
            _messageDispatcher.Stop();
            Assert.Throws<InvalidOperationException>(() => Dispatch(new DispatchCommand()));
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
    }
}