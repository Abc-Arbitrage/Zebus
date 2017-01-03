using System;
using System.Collections.Generic;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Dispatch.Pipes;
using Abc.Zebus.Testing.Dispatch;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Messages;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Dispatch.Pipes
{
    [TestFixture]
    public class PipeInvocationTests
    {
        private PipeInvocation _invocation;
        private IMessageHandlerInvocation _handlerInvocation;
        private TestMessageHandlerInvoker _invoker;
        private FakeCommand _message;
        private MessageContext _messageContext;
        private List<IPipe> _pipes;

        [SetUp]
        public void Setup()
        {
            _invoker = new TestMessageHandlerInvoker<FakeCommand>();
            _message = new FakeCommand(123);
            _messageContext = MessageContext.CreateTest("u.name");
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
        public void should_call_before_invoke_on_the_pipes_when_preparing_the_handler()
        {
            var beforeCalled = false;
            var handler = new Handler();

            _pipes.Add(new TestPipe
            {
                Name = "Pipe1",
                BeforeCallback = x => beforeCalled = true
            });

            using (_handlerInvocation.SetupForInvocation(handler))
            {
                beforeCalled.ShouldBeTrue();
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

            _invoker.InvokeMessageHandlerCallback = x => order.Add(3);

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

            _invoker.InvokeMessageHandlerCallback = x =>
            {
                throw new ArgumentException("Foo");
            };

            Assert.Throws<ArgumentException>(() =>
            {
                using (_handlerInvocation.SetupForInvocation())
                {
                    _invocation.Run();
                }
            });

            exception.ShouldNotBeNull();
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