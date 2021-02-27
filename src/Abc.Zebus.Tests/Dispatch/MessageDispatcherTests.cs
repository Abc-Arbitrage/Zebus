using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.DependencyInjection;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Dispatch.Pipes;
using Abc.Zebus.Routing;
using Abc.Zebus.Scan;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Dispatch.DispatchMessages;
using Abc.Zebus.Tests.Dispatch.Pipes;
using Abc.Zebus.Tests.Messages;
using Abc.Zebus.Tests.Scan;
using Abc.Zebus.Tests.Serialization;
using Abc.Zebus.Util;
using Moq;
using NUnit.Framework;
using StructureMap;

namespace Abc.Zebus.Tests.Dispatch
{
    [TestFixture]
    public partial class MessageDispatcherTests
    {
        private MessageDispatcher _messageDispatcher;
        private Mock<IContainer> _containerMock;
        private DispatchQueueFactory _dispatchQueueFactory;
        private PipeManager _pipeManager;

        [SetUp]
        public void Setup()
        {
            _containerMock = new Mock<IContainer>();
            _containerMock.Setup(x => x.GetInstance(It.IsAny<Type>())).Returns<Type>(Activator.CreateInstance);
            _pipeManager = new PipeManager(new IPipeSource[] { new PipeSource<TestPipe>(new Container()) });

            _dispatchQueueFactory = new DispatchQueueFactory(_pipeManager);

            _messageDispatcher = CreateDispatcher(_dispatchQueueFactory);
        }

        private MessageDispatcher CreateDispatcher(IDispatchQueueFactory dispatchQueueFactory)
        {
            var containerProvider = new StructureMapContainerProvider(_containerMock.Object);
            var invokerLoaders = new IMessageHandlerInvokerLoader[]
            {
                new SyncMessageHandlerInvokerLoader(containerProvider),
                new AsyncMessageHandlerInvokerLoader(containerProvider),
            };

            var messageDispatcher = new MessageDispatcher(invokerLoaders, dispatchQueueFactory);

            messageDispatcher.ConfigureAssemblyFilter(x => x == GetType().Assembly);
            messageDispatcher.ConfigureHandlerFilter(type => type != typeof(SyncMessageHandlerInvokerLoaderTests.WrongAsyncHandler));

            return messageDispatcher;
        }

        [Test]
        public void should_find_handled_message_type_from_simple_message_handler()
        {
            _messageDispatcher.LoadMessageHandlerInvokers();

            var invokers = _messageDispatcher.GetMessageHandlerInvokers();
            invokers.ShouldContain(x => x.MessageTypeId == new MessageTypeId(typeof(ScanCommand1)));
        }

        [Test]
        public void should_find_invokers_from_message_handler()
        {
            _messageDispatcher.LoadMessageHandlerInvokers();

            var invokers = _messageDispatcher.GetMessageHandlerInvokers().ToList();
            invokers.ShouldContain(x => x.MessageHandlerType == typeof(ScanCommandHandler1) && x.MessageType == typeof(ScanCommand1));
            invokers.ShouldContain(x => x.MessageHandlerType == typeof(ScanCommandHandler1) && x.MessageType == typeof(ScanCommand2));
        }

        [Test]
        public void should_not_auto_subscribe_to_no_scan_handlers()
        {
            Attribute.IsDefined(typeof(ScanCommandHandler2), typeof(NoScanAttribute)).ShouldBeTrue("ScanCommandHandler2 should be [NoScan]");

            _messageDispatcher.LoadMessageHandlerInvokers();

            var invoker = _messageDispatcher.GetMessageHandlerInvokers().Single(x => x.MessageHandlerType == typeof(ScanCommandHandler2));
            invoker.ShouldBeSubscribedOnStartup.ShouldBeFalse();
        }

        [Test]
        public void should_not_auto_subscribe_to_routable_commands()
        {
            Attribute.IsDefined(typeof(RoutableCommand), typeof(Routable)).ShouldBeTrue("RoutableCommand should be [Routable]");

            _messageDispatcher.LoadMessageHandlerInvokers();

            var invoker = _messageDispatcher.GetMessageHandlerInvokers().Single(x => x.MessageType == typeof(RoutableCommand));
            invoker.ShouldBeSubscribedOnStartup.ShouldBeFalse();
        }

        [Test]
        public void should_find_handled_message_type_only_once()
        {
            _messageDispatcher.LoadMessageHandlerInvokers();

            var types = _messageDispatcher.GetHandledMessageTypes();
            types.Count(x => x == new MessageTypeId(typeof(ScanCommand2))).ShouldEqual(1);
        }

        [Test]
        public void should_filter_assemblies()
        {
            _messageDispatcher.ConfigureAssemblyFilter(x => x.GetName().Name == "Abc.Zebus");
            _messageDispatcher.LoadMessageHandlerInvokers();

            var types = _messageDispatcher.GetMessageHandlerInvokers();
            types.ShouldNotContain(x => x.MessageType.Assembly.FullName == GetType().Assembly.FullName);
        }

        [Test]
        public void should_filter_handlers()
        {
            _messageDispatcher.ConfigureHandlerFilter(x => x == typeof(ScanCommandHandler1));
            _messageDispatcher.LoadMessageHandlerInvokers();

            var types = _messageDispatcher.GetMessageHandlerInvokers();
            types.ShouldNotContain(x => x.MessageHandlerType != typeof(ScanCommandHandler1));
        }

        [Test]
        public void should_filter_messages()
        {
            _messageDispatcher.ConfigureMessageFilter(x => x == typeof(ScanCommand1));
            _messageDispatcher.LoadMessageHandlerInvokers();

            var types = _messageDispatcher.GetHandledMessageTypes()
                                          .Select(x => x.GetMessageType())
                                          .ToList();

            types.ShouldContain(typeof(ScanCommand1));
            types.ShouldNotContain(typeof(ScanCommand2));
        }

        [Test]
        public void should_invoke_handle_method()
        {
            LoadAndStartDispatcher();

            var handler = new ScanCommandHandler1();
            _containerMock.Setup(x => x.GetInstance(typeof(ScanCommandHandler1))).Returns(handler);

            var command = new ScanCommand1();
            DispatchAndWaitForCompletion(command);

            handler.HandledCommand1.ShouldEqual(command);
        }

        [Test]
        public void should_invoke_both_sync_and_async_handlers()
        {
            LoadAndStartDispatcher();

            var asyncHandler = new AsyncCommandHandler { WaitForSignal = true };
            _containerMock.Setup(x => x.GetInstance(typeof(AsyncCommandHandler))).Returns(asyncHandler);

            var syncHandler = new SyncCommandHandler();
            _containerMock.Setup(x => x.GetInstance(typeof(SyncCommandHandler))).Returns(syncHandler);

            var command = new DispatchCommand();
            DispatchFromDefaultDispatchQueue(command);

            syncHandler.Called.ShouldBeTrue();
            asyncHandler.CalledSignal.Wait(50.Milliseconds()).ShouldBeFalse();

            command.Signal.Set();

            asyncHandler.CalledSignal.Wait(10.Seconds()).ShouldBeTrue();
        }

        [Test]
        public void should_filter_invoker()
        {
            LoadAndStartDispatcher();

            var asyncHandler = new AsyncCommandHandler();
            _containerMock.Setup(x => x.GetInstance(typeof(AsyncCommandHandler))).Returns(asyncHandler);

            var syncHandler = new SyncCommandHandler();
            _containerMock.Setup(x => x.GetInstance(typeof(SyncCommandHandler))).Returns(syncHandler);

            var context = MessageContext.CreateTest("u.name");
            var command = new DispatchCommand();
            var dispatched = new ManualResetEvent(false);
            var dispatch = new MessageDispatch(context, command, new TestMessageSerializer(), (x, r) => dispatched.Set());
            _messageDispatcher.Dispatch(dispatch, x => x == typeof(AsyncCommandHandler));

            dispatched.WaitOne(5.Seconds()).ShouldBeTrue();

            syncHandler.Called.ShouldBeFalse();
            asyncHandler.CalledSignal.IsSet.ShouldBeTrue();
        }

        [Test]
        public void should_build_and_run_pipe_invocation()
        {
            LoadAndStartDispatcher();

            _pipeManager.EnablePipe("TestPipe");
            var pipe = (TestPipe)_pipeManager.GetEnabledPipes(typeof(ScanCommandHandler1)).ExpectedSingle();

            var command = new ScanCommand1();
            DispatchAndWaitForCompletion(command);

            pipe.BeforeInvokeArgs.ShouldNotBeNull();
            pipe.AfterInvokeArgs.ShouldNotBeNull();
        }

        [Test]
        public void should_build_and_run_pipe_invocation_async()
        {
            LoadAndStartDispatcher();

            _pipeManager.EnablePipe("TestPipe");
            var pipe = (TestPipe)_pipeManager.GetEnabledPipes(typeof(AsyncCommandHandler)).ExpectedSingle();

            var command = new AsyncCommand();
            DispatchAndWaitForCompletion(command);

            pipe.BeforeInvokeArgs.ShouldNotBeNull();
            pipe.AfterInvokeArgs.ShouldNotBeNull();
        }

        [Test]
        public void should_catch_exceptions()
        {
            LoadAndStartDispatcher();

            var command = new FailingCommand(new InvalidOperationException(":'("));
            var result = DispatchAndWaitForCompletion(command);

            var error = result.Errors.ExpectedSingle();
            error.ShouldEqual(command.Exception);
        }

        [Test]
        public void should_catch_async_exceptions()
        {
            LoadAndStartDispatcher();

            var command = new AsyncFailingCommand(new InvalidOperationException(":'("));
            var result = DispatchAndWaitForCompletion(command);

            var error = result.Errors.ExpectedSingle();
            error.ShouldEqual(command.Exception);
        }



        [Test]
        public void should_catch_async_exceptions_thrown_synchronously()
        {
            LoadAndStartDispatcher();

            var command = new AsyncFailingCommand(new InvalidOperationException(":'("))
            {
                ThrowSynchronously = true
            };

            var result = DispatchAndWaitForCompletion(command);

            var error = result.Errors.ExpectedSingle();
            error.ShouldEqual(command.Exception);
        }

        [Test]
        public void should_have_only_one_failing_handler()
        {
            _messageDispatcher.LoadMessageHandlerInvokers();

            var invokersCount = _messageDispatcher.GetMessageHandlerInvokers().Count(x => x.MessageType == typeof(AsyncFailingCommand));

            invokersCount.ShouldEqual(1);
        }

        [Test]
        public void should_fail_dispatch_if_dispatching_to_an_handler_that_does_not_start_its_task()
        {
            LoadAndStartDispatcher();

            var command = new AsyncDoNotStartTaskCommand();
            var result = DispatchAndWaitForCompletion(command);

            result.Errors.Count.ShouldEqual(1);
        }

        [Test]
        public void should_dispatch_to_event_handler()
        {
            LoadAndStartDispatcher();

            IMessage receivedMessage = null;

            _messageDispatcher.AddInvoker(new DynamicMessageHandlerInvoker(x => receivedMessage = x, typeof(FakeEvent), new[] { BindingKey.Empty }));

            var evt = new FakeEvent(123);
            DispatchAndWaitForCompletion(evt);

            receivedMessage.ShouldEqual(evt);
        }

        [Test]
        public void should_get_reply_code()
        {
            LoadAndStartDispatcher();

            int? replyCode = null;
            var context = MessageContext.CreateTest();
            var dispatch = new MessageDispatch(context, new ReplyCommand(), new TestMessageSerializer(), (x, r) => replyCode = context.ReplyCode);
            _messageDispatcher.Dispatch(dispatch);

            Wait.Until(() => replyCode == ReplyCommand.ReplyCode, 2.Seconds());
        }

        [Test]
        public void should_purge_dispatch_queues()
        {
            LoadAndStartDispatcher();

            var firstMessage = new ExecutableEvent { IsBlocking = true };

            Dispatch(firstMessage);

            firstMessage.HandleStarted.Wait(2.Seconds()).ShouldBeTrue();

            Dispatch(new ExecutableEvent());
            Dispatch(new ExecutableEvent());
            Dispatch(new ExecutableEvent());

            var dispatchQueue = _dispatchQueueFactory.DispatchQueues.ExpectedSingle(x => x.Name == DispatchQueueNameScanner.DefaultQueueName);
            dispatchQueue.QueueLength.ShouldEqual(3);

            var purgeCount = _messageDispatcher.Purge();

            purgeCount.ShouldEqual(3);
            dispatchQueue.QueueLength.ShouldEqual(0);

            firstMessage.Unblock();
        }

        [Test]
        public void should_hide_task_scheduler()
        {
            LoadAndStartDispatcher();

            var syncHandler = new CapturingTaskSchedulerSyncCommandHandler();
            _containerMock.Setup(x => x.GetInstance(typeof(CapturingTaskSchedulerSyncCommandHandler))).Returns(syncHandler);

            var asyncHandler = new CapturingTaskSchedulerAsyncCommandHandler();
            _containerMock.Setup(x => x.GetInstance(typeof(CapturingTaskSchedulerAsyncCommandHandler))).Returns(asyncHandler);

            var command = new DispatchCommand();
            Dispatch(command);

            syncHandler.Signal.WaitOne(10.Seconds()).ShouldBeTrue();
            asyncHandler.Signal.WaitOne(10.Seconds()).ShouldBeTrue();

            syncHandler.TaskScheduler.ShouldEqual(TaskScheduler.Default);
            asyncHandler.TaskScheduler.ShouldEqual(TaskScheduler.Default);
        }

        [Test]
        public void should_publish_starting_event_before_dispatch()
        {
            _messageDispatcher.LoadMessageHandlerInvokers();

            var startingSignal = new ManualResetEvent(false);
            var startingHangSignal = new ManualResetEvent(false);
            _messageDispatcher.Starting += () =>
            {
                startingSignal.Set();
                startingHangSignal.WaitOne(10.Seconds()).ShouldBeTrue();
            };

            var dispatchTask = Dispatch(new ExecutableEvent());
            var startingTask = Task.Run(() => _messageDispatcher.Start());

            startingSignal.WaitOne(1.Second()).ShouldBeTrue();
            dispatchTask.Wait(200.Milliseconds()).ShouldBeFalse();

            startingHangSignal.Set();
            startingTask.Wait(1.Second()).ShouldBeTrue();

            dispatchTask.Wait(1.Second()).ShouldBeTrue();
        }

        private void LoadAndStartDispatcher()
        {
            _messageDispatcher.LoadMessageHandlerInvokers();
            _messageDispatcher.Start();
        }

        private DispatchResult DispatchAndWaitForCompletion(IMessage message)
        {
            var dispatch = Dispatch(message);

            dispatch.Wait(20.Seconds()).ShouldBeTrue("Dispatch should be completed");

            return dispatch.Result;
        }

        private void DispatchFromDefaultDispatchQueue(IMessage message)
        {
            DispatchAndWaitForCompletion(new ExecutableEvent { Callback = x => Dispatch(message) });
        }

        private Task<DispatchResult> Dispatch(IMessage message, bool isLocal = false)
        {
            var taskCompletionSource = new TaskCompletionSource<DispatchResult>();

            var dispatch = new MessageDispatch(MessageContext.CreateTest("u.name"), message, new TestMessageSerializer(), (x, r) => taskCompletionSource.SetResult(r))
            {
                IsLocal = isLocal
            };

            _messageDispatcher.Dispatch(dispatch);

            return taskCompletionSource.Task;
        }

        private class DispatchQueueFactory : IDispatchQueueFactory
        {
            private readonly PipeManager _pipeManager;

            public DispatchQueueFactory(PipeManager pipeManager)
            {
                _pipeManager = pipeManager;
            }

            public List<DispatchQueue> DispatchQueues { get; } = new List<DispatchQueue>();

            public DispatchQueue Create(string queueName)
            {
                var taskScheduler = new DispatchQueue(_pipeManager, 200, queueName);
                DispatchQueues.Add(taskScheduler);
                return taskScheduler;
            }
        }
    }
}
