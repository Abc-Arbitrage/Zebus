using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Dispatch.Pipes;
using Abc.Zebus.Routing;
using Abc.Zebus.Scan;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Dispatch;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Dispatch.DispatchMessages;
using Abc.Zebus.Tests.Messages;
using Abc.Zebus.Tests.Scan;
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
        private Mock<IPipeManager> _pipeManagerMock;
        private TestPipeInvocation _invocation;
        private volatile DispatchResultRef _dispatchResultRef;
        private DispatcherTaskSchedulerFactory _taskSchedulerFactory;

        [SetUp]
        public void Setup()
        {
            _containerMock = new Mock<IContainer>();
            _containerMock.Setup(x => x.GetInstance(It.IsAny<Type>())).Returns<Type>(Activator.CreateInstance);

            _invocation = null;
            _pipeManagerMock = new Mock<IPipeManager>();
            _pipeManagerMock.Setup(x => x.BuildPipeInvocation(It.IsAny<IMessageHandlerInvoker>(), It.IsAny<IMessage>(), It.IsAny<MessageContext>()))
                            .Returns<IMessageHandlerInvoker, IMessage, MessageContext>((invoker, message, messageContext) =>
                            {
                                _invocation = new TestPipeInvocation(message, messageContext, invoker);
                                return _invocation;
                            });

            _taskSchedulerFactory = new DispatcherTaskSchedulerFactory();

            _messageDispatcher = CreateAndStartDispatcher(_taskSchedulerFactory);
        }

        private MessageDispatcher CreateAndStartDispatcher(IDispatcherTaskSchedulerFactory taskSchedulerFactory)
        {
            var messageDispatcher = new MessageDispatcher(_pipeManagerMock.Object, new IMessageHandlerInvokerLoader[]
            {
                new SyncMessageHandlerInvokerLoader(_containerMock.Object),
                new AsyncMessageHandlerInvokerLoader(_containerMock.Object),
            }, taskSchedulerFactory);

            messageDispatcher.ConfigureAssemblyFilter(x => x == GetType().Assembly);
            messageDispatcher.ConfigureHandlerFilter(type => type != typeof(SyncMessageHandlerInvokerLoaderTests.WrongAsyncHandler));
            messageDispatcher.Start();
            return messageDispatcher;
        }

        private class DispatcherTaskSchedulerFactory : IDispatcherTaskSchedulerFactory
        {
            private readonly IList<DispatcherTaskScheduler> _taskSchedulers = new List<DispatcherTaskScheduler>();

            public IList<DispatcherTaskScheduler> TaskSchedulers
            {
                get { return _taskSchedulers; }
            }

            public DispatcherTaskScheduler Create(string queueName)
            {
                var taskScheduler = new DispatcherTaskScheduler(queueName);
                TaskSchedulers.Add(taskScheduler);
                return taskScheduler;
            }
        }

        [Test]
        public void should_find_handled_message_type_from_simple_message_handler()
        {
            _messageDispatcher.LoadMessageHandlerInvokers();

            var invokers = _messageDispatcher.GetMessageHanlerInvokers();
            invokers.ShouldContain(x => x.MessageTypeId == new MessageTypeId(typeof(ScanCommand1)));
        }

        [Test]
        public void should_find_invokers_from_message_handler()
        {
            _messageDispatcher.LoadMessageHandlerInvokers();

            var invokers = _messageDispatcher.GetMessageHanlerInvokers().ToList();
            invokers.ShouldContain(x => x.MessageHandlerType == typeof(ScanCommandHandler1) && x.MessageType == typeof(ScanCommand1));
            invokers.ShouldContain(x => x.MessageHandlerType == typeof(ScanCommandHandler1) && x.MessageType == typeof(ScanCommand2));
        }

        [Test]
        public void should_not_auto_subscribe_to_no_scan_handlers()
        {
            Attribute.IsDefined(typeof(ScanCommandHandler2), typeof(NoScanAttribute)).ShouldBeTrue("ScanCommandHandler2 should be [NoScan]");

            _messageDispatcher.LoadMessageHandlerInvokers();

            var invoker = _messageDispatcher.GetMessageHanlerInvokers().Single(x => x.MessageHandlerType == typeof(ScanCommandHandler2));
            invoker.ShouldBeSubscribedOnStartup.ShouldBeFalse();
        }

        [Test]
        public void should_not_auto_subscribe_to_routable_commands()
        {
            Attribute.IsDefined(typeof(RoutableCommand), typeof(Routable)).ShouldBeTrue("RoutableCommand should be [Routable]");

            _messageDispatcher.LoadMessageHandlerInvokers();

            var invoker = _messageDispatcher.GetMessageHanlerInvokers().Single(x => x.MessageType == typeof(RoutableCommand));
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

            var types = _messageDispatcher.GetMessageHanlerInvokers();
            types.ShouldNotContain(x => x.MessageType.Assembly.FullName == GetType().Assembly.FullName);
        }

        [Test]
        public void should_filter_handlers()
        {
            _messageDispatcher.ConfigureHandlerFilter(x => x == typeof(ScanCommandHandler1));
            _messageDispatcher.LoadMessageHandlerInvokers();

            var types = _messageDispatcher.GetMessageHanlerInvokers();
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
            _messageDispatcher.LoadMessageHandlerInvokers();

            var handler = new ScanCommandHandler1();
            _containerMock.Setup(x => x.GetInstance(typeof(ScanCommandHandler1))).Returns(handler);

            var command = new ScanCommand1();
            Dispatch(command);

            handler.HandledCommand1.ShouldEqual(command);
        }

        [Test]
        public void should_invoke_both_sync_and_async_handlers()
        {
            _messageDispatcher.LoadMessageHandlerInvokers();

            var asyncHandler = new AsyncCommandHandler { WaitForSignal = true };
            _containerMock.Setup(x => x.GetInstance(typeof(AsyncCommandHandler))).Returns(asyncHandler);

            var syncHandler = new SyncCommandHandler();
            _containerMock.Setup(x => x.GetInstance(typeof(SyncCommandHandler))).Returns(syncHandler);

            var command = new DispatchCommand();
            Dispatch(command);

            syncHandler.Called.ShouldBeTrue();
            asyncHandler.CalledSignal.Wait(50.Milliseconds()).ShouldBeFalse();

            command.Signal.Set();

            asyncHandler.CalledSignal.Wait(1000.Milliseconds()).ShouldBeTrue();
        }

        [Test]
        public void should_filter_invoker()
        {
            _messageDispatcher.LoadMessageHandlerInvokers();

            var asyncHandler = new AsyncCommandHandler();
            _containerMock.Setup(x => x.GetInstance(typeof(AsyncCommandHandler))).Returns(asyncHandler);

            var syncHandler = new SyncCommandHandler();
            _containerMock.Setup(x => x.GetInstance(typeof(SyncCommandHandler))).Returns(syncHandler);

            var context = MessageContext.CreateTest("u.name");
            var command = new DispatchCommand();
            var dispatched = new ManualResetEvent(false);
            var dispatch = new MessageDispatch(context, command, (x, r) => dispatched.Set());
            _messageDispatcher.Dispatch(dispatch, x => x == typeof(AsyncCommandHandler));

            dispatched.WaitOne(500.Milliseconds()).ShouldBeTrue();

            syncHandler.Called.ShouldBeFalse();
            asyncHandler.CalledSignal.IsSet.ShouldBeTrue();
        }

        [Test]
        public void should_build_and_run_pipe_invocation()
        {
            _messageDispatcher.LoadMessageHandlerInvokers();

            var command = new ScanCommand1();
            Dispatch(command);

            _invocation.ShouldNotBeNull();
            _invocation.WasRun.ShouldBeTrue();
        }

        [Test]
        public void should_build_and_run_pipe_invocation_async()
        {
            _messageDispatcher.LoadMessageHandlerInvokers();

            var command = new AsyncCommand();
            Dispatch(command);

            _invocation.ShouldNotBeNull();
            _invocation.WasRunAsync.ShouldBeTrue();
        }

        [Test]
        public void should_catch_exceptions()
        {
            _messageDispatcher.LoadMessageHandlerInvokers();

            var command = new FailingCommand(new InvalidOperationException(":'("));
            Dispatch(command);

            var error = _dispatchResultRef.Value.Errors.ExpectedSingle();
            error.ShouldEqual(command.Exception);
        }

        [Test]
        public void should_catch_async_exceptions()
        {
            _messageDispatcher.LoadMessageHandlerInvokers();

            var command = new AsyncFailingCommand(new InvalidOperationException(":'("));
            Dispatch(command);

            Wait.Until(() => _dispatchResultRef != null, 2.Seconds());

            var error = _dispatchResultRef.Value.Errors.ExpectedSingle();
            error.ShouldEqual(command.Exception);
        }

        [Test]
        public void should_have_only_one_failing_handler()
        {
            _messageDispatcher.LoadMessageHandlerInvokers();
            
            var invokersCount = _messageDispatcher.GetMessageHanlerInvokers().Count(x => x.MessageType == typeof(AsyncFailingCommand));

            invokersCount.ShouldEqual(1);
        }

        [Test]
        public void should_fail_dispatch_if_dispatching_to_an_handler_that_does_not_start_its_task()
        {
            _messageDispatcher.LoadMessageHandlerInvokers();

            var command = new AsyncDoNotStartTaskCommand();
            Dispatch(command);

            _dispatchResultRef.ShouldNotBeNull();
            _dispatchResultRef.Value.Errors.Count().ShouldEqual(1);
        }

        [Test]
        public void should_dispatch_to_event_handler()
        {
            IMessage receivedMessage = null;
            _messageDispatcher.AddInvoker(new EventHandlerInvoker(x => receivedMessage = x, typeof(FakeEvent)));

            var evt = new FakeEvent(123);
            Dispatch(evt);

            receivedMessage.ShouldEqual(evt);
        }

        [Test]
        public void should_get_reply_code()
        {
            _messageDispatcher.LoadMessageHandlerInvokers();

            var context = MessageContext.CreateTest("u.name");
            var dispatch = new MessageDispatch(context.WithDispatchQueueName(DispatchQueueNameScanner.DefaultQueueName), new ReplyCommand(), (x, r) => { });
            _messageDispatcher.Dispatch(dispatch);

            context.ReplyCode.ShouldEqual(ReplyCommand.ReplyCode);
        }

        [Test]
        public void should_purge_dispatch_queues()
        {
            var taskSchedulerMock = new Mock<DispatcherTaskScheduler>();
            taskSchedulerMock.Setup(scheduler => scheduler.PurgeTasks()).Returns(1);
            var taskSchedulerFactoryMock = new Mock<IDispatcherTaskSchedulerFactory>();
            taskSchedulerFactoryMock.Setup(factory => factory.Create(It.IsAny<string>())).Returns(taskSchedulerMock.Object);
            var messageDispatcher = CreateAndStartDispatcher(taskSchedulerFactoryMock.Object);
            messageDispatcher.LoadMessageHandlerInvokers();
            Dispatch(new DispatchCommand(), messageDispatcher);
            
            var purgedMessagesCount = messageDispatcher.Purge();

            purgedMessagesCount.ShouldEqual(3);
            taskSchedulerMock.Verify(scheduler => scheduler.PurgeTasks(), Times.Exactly(3));
        }

        [Test]
        public void should_hide_task_scheduler()
        {
            _messageDispatcher.LoadMessageHandlerInvokers();

            var syncHandler = new CapturingTaskSchedulerSyncCommandHandler();
            _containerMock.Setup(x => x.GetInstance(typeof(CapturingTaskSchedulerSyncCommandHandler))).Returns(syncHandler);

            var asyncHandler = new CapturingTaskSchedulerAsyncCommandHandler();
            _containerMock.Setup(x => x.GetInstance(typeof(CapturingTaskSchedulerAsyncCommandHandler))).Returns(asyncHandler);
            
            var command = new DispatchCommand();
            Dispatch(command, MessageContext.CreateTest("u.name").WithDispatchQueueName("some queue")); // make sure we go through a dispatch queue!

            syncHandler.Signal.WaitOne(1.Second()).ShouldBeTrue();
            asyncHandler.Signal.WaitOne(1.Second()).ShouldBeTrue();

            syncHandler.TaskScheduler.ShouldEqual(TaskScheduler.Default);
            asyncHandler.TaskScheduler.ShouldEqual(TaskScheduler.Default);
        }

        private void Dispatch(IMessage message, MessageDispatcher dispatcher = null)
        {
            var messageContext = MessageContext.CreateTest("u.name");
            Dispatch(message, messageContext.WithDispatchQueueName(DispatchQueueNameScanner.DefaultQueueName), dispatcher);
        }

        private void Dispatch(IMessage message, MessageContext context, MessageDispatcher dispatcher = null)
        {
            _dispatchResultRef = null;

            var dispatch = new MessageDispatch(context, message, (x, r) => _dispatchResultRef = new DispatchResultRef(r));
            (dispatcher ?? _messageDispatcher).Dispatch(dispatch);
        }

        private class DispatchResultRef
        {
            public readonly DispatchResult Value;

            public DispatchResultRef(DispatchResult value)
            {
                Value = value;
            }
        }
    }
}