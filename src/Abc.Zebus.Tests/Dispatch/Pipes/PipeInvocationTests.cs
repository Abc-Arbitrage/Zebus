using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Dispatch.Pipes;
using Abc.Zebus.Testing.Dispatch;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Dispatch.DispatchMessages;
using Abc.Zebus.Tests.Messages;
using Abc.Zebus.Util;
using Abc.Zebus.Util.Extensions;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Dispatch.Pipes
{
    [TestFixture]
    public class PipeInvocationTests
    {
        private PipeInvocation _invocation;
        private IMessageHandlerInvocation _handlerInvocation;
        private TestMessageHandlerInvoker<ExecutableEvent> _invoker;
        private ExecutableEvent _message;
        private MessageContext _messageContext;
        private List<IPipe> _pipes;

        [SetUp]
        public void Setup()
        {
            _invoker = new TestMessageHandlerInvoker<ExecutableEvent>();
            _message = new ExecutableEvent();
            _messageContext = MessageContext.CreateTest();
            _pipes = new List<IPipe>();
            _invocation = new PipeInvocation(_invoker, new List<IMessage> { _message }, _messageContext, _pipes);
            _handlerInvocation = _invocation;
        }

        [Test]
        public void should_inject_message_context_to_handler()
        {
            var handler = new Handler();

            using (_handlerInvocation.SetupForInvocation(handler))
            {
                handler.Context.ShouldEqual(_messageContext);
            }
        }

        [Test]
        public void should_apply_mutations()
        {
            var handler = new Handler();

            _invocation.AddHandlerMutation(x => ((Handler)x).Value = 42);
            using (_handlerInvocation.SetupForInvocation(handler))
            {
                _invocation.Run();

                handler.Value.ShouldEqual(42);
            }
        }

        [Test]
        public void should_invoke_pipes_in_order()
        {
            var order = new List<int>();

            _pipes.Add(new TestPipe
            {
                Name = "Pipe1",
                BeforeCallback = x =>
                {
                    order.Add(1);
                    x.State = "Pipe 1 state";
                },
                AfterCallback = x =>
                {
                    order.Add(5);
                    x.State.ShouldEqual("Pipe 1 state");
                },
            });

            _pipes.Add(new TestPipe
            {
                Name = "Pipe2",
                BeforeCallback = x =>
                {
                    order.Add(2);
                    x.State = "Pipe 2 state";
                },
                AfterCallback = x =>
                {
                    order.Add(4);
                    x.State.ShouldEqual("Pipe 2 state");
                },
            });

            _message.Callback = x => order.Add(3);

            _invocation.Run();

            order.Count.ShouldEqual(5);
            order.ShouldBeOrdered();
        }

        [Test]
        public void should_get_exception()
        {
            Exception exception = null;

            _pipes.Add(new TestPipe
            {
                AfterCallback = x => exception = x.Exception
            });

            _message.Callback = _ => throw new ArgumentException("Foo");

            Assert.Throws<ArgumentException>(() =>
            {
                using (_handlerInvocation.SetupForInvocation())
                {
                    _invocation.Run();
                }
            });

            exception.ShouldNotBeNull();
        }

        [Test]
        public async Task should_get_exception_on_cancellation()
        {
            var invoker = new TestAsyncMessageHandlerInvoker<AsyncExecutableEvent>();
            var message = new AsyncExecutableEvent
            {
                Callback = async _ =>
                {
                    await Task.Yield();
                    throw new OperationCanceledException();
                }
            };

            _invocation = new PipeInvocation(invoker, new List<IMessage> { message }, _messageContext, _pipes);
            _handlerInvocation = _invocation;

            var afterInvokeArgsTask = new TaskCompletionSource<AfterInvokeArgs>();

            _pipes.Add(new TestPipe
            {
                AfterCallback = args => afterInvokeArgsTask.SetResult(args)
            });

            using (_handlerInvocation.SetupForInvocation())
            {
                Assert.ThrowsAsync<OperationCanceledException>(async () => await _invocation.RunAsync().WithTimeoutAsync(5.Seconds()));
            }

            var result = await afterInvokeArgsTask.Task.WithTimeoutAsync(5.Seconds());

            result.IsFaulted.ShouldBeTrue();
            result.Exception.ShouldBeNull(); // Not great, buts lets detect a cancellation
        }

        public class Handler : IMessageHandler<FakeCommand>, IMessageContextAware
        {
            public int Value { get; set; }
            public MessageContext Context { get; set; }

            public void Handle(FakeCommand message)
            {
            }
        }
    }
}
