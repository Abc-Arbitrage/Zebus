using System;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Core;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
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

        [Test]
        public void should_instanciate_new_message_context_aware_bus_for_every_handler_without_race_conditions()
        {
            var handlerData = new TestAsyncHandlerHelper();

            var container = new Container(x =>
            {
                x.ForSingletonOf<IBus>().Use(new TestBus());
                x.ForSingletonOf<TestAsyncHandlerHelper>().Use(handlerData);
            });

            var invoker = new AsyncMessageHandlerInvoker(container, typeof(TestAsyncHandler), typeof(ScanCommand1));

            var messageContext1 = MessageContext.CreateOverride(new PeerId("Abc.Testing.0"), null);
            var messageContext2 = MessageContext.CreateOverride(new PeerId("Abc.Testing.1"), null);

            var handler1 = Task.Run(() => (TestAsyncHandler)invoker.CreateHandler(messageContext1));
            var handler2 = Task.Run(() => (TestAsyncHandler)invoker.CreateHandler(messageContext2));

            Wait.Until(() => handlerData.WaitingHandlerCount == 2, 1.Second());
            handlerData.Release();

            Task.WaitAll(new Task[] { handler1, handler2 }, 1.Second());

            handler1.Result.Bus.ShouldNotEqual(handler2.Result.Bus);
            ((MessageContextAwareBus)handler1.Result.Bus).InnerBus.ShouldEqual(((MessageContextAwareBus)handler2.Result.Bus).InnerBus);
        }

        private class ErroringAsyncHandler : IAsyncMessageHandler<ScanCommand1>
        {
            public Task Handle(ScanCommand1 message)
            {
                throw new InvalidOperationException();
            }
        }

        private class TestAsyncHandlerHelper
        {
            private readonly TaskCompletionSource<object> _tcs = new TaskCompletionSource<object>();
            public Task WaitTask => _tcs.Task;

            public int WaitingHandlerCount;

            public void Release() => _tcs.SetResult(null);
        }

        private class TestAsyncHandler : IAsyncMessageHandler<ScanCommand1>, IMessageContextAware
        {
            public readonly IBus Bus;

            public TestAsyncHandler(IBus bus, TestAsyncHandlerHelper handlerHelper)
            {
                Bus = bus;

                Interlocked.Increment(ref handlerHelper.WaitingHandlerCount);
                handlerHelper.WaitTask.Wait();
            }

            public Task Handle(ScanCommand1 message)
            {
                return Task.FromResult(42);
            }

            public MessageContext Context { get; set; }
        }
    }
}
