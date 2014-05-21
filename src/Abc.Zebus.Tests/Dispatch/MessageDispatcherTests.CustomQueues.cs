using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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

            Dispatch(new DispatchCommand());

            syncHandler.Called.ShouldBeTrue("Sync handler should be run synchronously");

            Wait.Until(() => handler1List.Count == 1 && handler1List[0].HandleStarted, 150.Milliseconds(), "First handler should be started");
            Wait.Until(() => handler2List.Count == 1 && handler2List[0].HandleStarted, 150.Milliseconds(), "second handler should be started");

            syncHandler.Called = false;
            Dispatch(new DispatchCommand());

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

            var handler = new SyncCommandHandlerWithQueueName1() { WaitForSignal = true };
            _containerMock.Setup(x => x.GetInstance(typeof(SyncCommandHandlerWithQueueName1))).Returns(handler);

            var task = Task.Run(() =>
            {
                var context = MessageContext.CreateTest("u.name").WithDispatchQueueName("DispatchQueue1");
                Dispatch(new DispatchCommand(), context);
            });

            Thread.Sleep(150);
            task.IsCompleted.ShouldBeFalse("Dispatch should run synchronously");

            handler.CalledSignal.Set();
            Wait.Until(() => task.IsCompleted, 150.Milliseconds());
        }

        [Test]
        public void should_not_hang_when_running_invoker_synchronously_in_same_dispatch_queue()
        {
            _messageDispatcher.ConfigureHandlerFilter(x => x == typeof(ForwardCommandHandler) || x == typeof(SyncCommandHandlerWithQueueName1));
            _messageDispatcher.LoadMessageHandlerInvokers();

            var handler1 = new ForwardCommandHandler { Action = x => Dispatch(new DispatchCommand(), x) };
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

            Wait.Until(() => handler.DispatchQueueName != null, 150.Milliseconds());

            var queueSource = new UseOtherQueue();
            handler.DispatchQueueName.ShouldEqual(queueSource.QueueName);
        }

        [Test]
        public void should_wait_for_TaskSchedulers_to_stop()
        {
            MakeSureOneTaskSchedulerIsActive();

            var canStop = new ManualResetEvent(false);
            var tasksAreWaiting = new CountdownEvent(3);
            var count = 0;

            foreach (var taskScheduler in _taskSchedulerFactory.TaskSchedulers)
            {
               Interlocked.Increment(ref count);
                StartTask(() =>
                {
                    tasksAreWaiting.Signal();
                    canStop.WaitOne();
                    Interlocked.Decrement(ref count);
                }, taskScheduler);
            }

            tasksAreWaiting.Wait();

            var stopTask = Task.Factory.StartNew(() => _messageDispatcher.Stop());
            Wait.Until(() => stopTask.Status == TaskStatus.Running, 5);
            stopTask.IsCompleted.ShouldBeFalse();
            
            canStop.Set();
            
            stopTask.Wait(1.Second()).ShouldBeTrue();
            count.ShouldEqual(0);
        }

        private void MakeSureOneTaskSchedulerIsActive()
        {
            _messageDispatcher.LoadMessageHandlerInvokers();
            var handler = new SyncCommandHandlerWithOtherQueueName();
            _containerMock.Setup(x => x.GetInstance(typeof(SyncCommandHandlerWithOtherQueueName))).Returns(handler);
            Dispatch(new DispatchCommand());

            _taskSchedulerFactory.TaskSchedulers.Count.ShouldBeGreaterOrEqualThan(1);
        }
        
        [Test]
        public void should_throw_exception_if_calling_dispatched_when_stopped()
        {
            _messageDispatcher.Stop();
            Assert.Throws<InvalidOperationException>(() => Dispatch(new DispatchCommand()));
        }


        private void StartTask(Action action, TaskScheduler taskScheduler = null)
        {
            if (taskScheduler == null)
                taskScheduler = TaskScheduler.Default;

            Task.Factory.StartNew(action, new CancellationToken(), TaskCreationOptions.None, taskScheduler);
        }

        [Test]
        public void should_restart_TaskSchedulers()
        {
            MakeSureOneTaskSchedulerIsActive();

            _messageDispatcher.Stop();
            _messageDispatcher.Start();

            foreach (var taskScheduler in _taskSchedulerFactory.TaskSchedulers)
            {
                taskScheduler.IsRunning.ShouldBeTrue();
            }

        }
    }
}