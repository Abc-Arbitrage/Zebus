using System;
using System.Threading.Tasks;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Testing;
using Abc.Zebus.Tests.Dispatch.DispatchMessages;
using Abc.Zebus.Util;
using Moq;
using NUnit.Framework;
using StructureMap;

namespace Abc.Zebus.Tests.Dispatch
{
    [TestFixture]
    public class AsyncMessageHandlerInvokerTests
    {
        [Test]
        public void should_return_a_faulted_task_if_a_handler_throws()
        {
            var container = new Container(x =>
            {
                x.For<IBus>().Use(new Mock<IBus>().Object);
                x.ForSingletonOf<ErroringAsyncHandler>().Use(new ErroringAsyncHandler());
            });
            var handlerInvoker = new AsyncMessageHandlerInvoker(container, typeof(ErroringAsyncHandler), typeof(ScanCommand1));
            var messageContext = MessageContext.CreateOverride(new PeerId("Abc.Testing.0"), null);
            var invocation = new ScanCommand1().ToInvocation(messageContext);

            var invocationTask = handlerInvoker.InvokeMessageHandlerAsync(invocation);

            Wait.Until(() => invocationTask.Status == TaskStatus.Faulted, 1.Second());
        }

        private class ErroringAsyncHandler : IAsyncMessageHandler<ScanCommand1>
        {
            public Task Handle(ScanCommand1 message)
            {
                throw new InvalidOperationException();
            }
        }
    }
}