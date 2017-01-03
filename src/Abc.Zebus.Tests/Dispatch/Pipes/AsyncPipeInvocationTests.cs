using System;
using System.Collections.Generic;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Dispatch.Pipes;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Dispatch;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Messages;
using Abc.Zebus.Util;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Dispatch.Pipes
{
    [TestFixture]
    public class AsyncPipeInvocationTests
    {
        private PipeInvocation _invocation;
        private TestAsyncMessageHandlerInvoker _invoker;
        private FakeCommand _message;
        private MessageContext _messageContext;
        private List<IPipe> _pipes;

        [SetUp]
        public void Setup()
        {
            _invoker = new TestAsyncMessageHandlerInvoker<FakeCommand>();
            _message = new FakeCommand(123);
            _messageContext = MessageContext.CreateTest("u.name");
            _pipes = new List<IPipe>();
            _invocation = new PipeInvocation(_invoker, new List<IMessage> { _message }, _messageContext, _pipes);
        }

        [Test]
        public void should_invoke_handler_async()
        {
            IMessageHandlerInvocation capturedMessageHandlerInvocation = null;
            _invoker.InvokeMessageHandlerCallback = x => capturedMessageHandlerInvocation = x;

            _invocation.RunAsync().Wait();

            _invoker.Invoked.ShouldBeTrue();
            capturedMessageHandlerInvocation.ShouldEqual(_invocation);
        }

        [Test]
        public void should_invoke_pipes_in_order_async()
        {
            var order = new List<int>();

            _pipes.Add(new TestPipe
            {
                Name = "Pipe1",
                BeforeCallback = x => order.Add(1),
                AfterCallback = x => order.Add(5),
            });

            _pipes.Add(new TestPipe
            {
                Name = "Pipe2",
                BeforeCallback = x => order.Add(2),
                AfterCallback = x => order.Add(4),
            });

            _invoker.InvokeMessageHandlerCallback = x => order.Add(3);

            _invocation.RunAsync().Wait();

            Wait.Until(() => order.Count == 5, 500.Milliseconds());
            order.ShouldBeOrdered();
        }

        [Test]
        public void should_get_exception_async()
        {
            Exception exceptionFromPipe = null;

            _pipes.Add(new TestPipe
            {
                AfterCallback = x => exceptionFromPipe = x.Exception
            });

            _invoker.InvokeMessageHandlerCallback = x =>
            {
                throw new ArgumentException("Foo");
            };

            Exception exceptionFromInvocation = null;
            _invocation.RunAsync().ContinueWith(t => exceptionFromInvocation = t.Exception.InnerExceptions.ExpectedSingle()).Wait();

            exceptionFromPipe.ShouldNotBeNull();
            exceptionFromInvocation.ShouldNotBeNull();
        }
    }
}