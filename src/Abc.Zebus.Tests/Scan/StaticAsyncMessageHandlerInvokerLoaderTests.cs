using System;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.DependencyInjection;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Scan;
using Abc.Zebus.Testing.Extensions;
using NUnit.Framework;
using StructureMap;

namespace Abc.Zebus.Tests.Scan
{
    [TestFixture]
    public class StaticAsyncMessageHandlerInvokerLoaderTests
    {
        [Test]
        public void should_load_async_invoker()
        {
            var invokerLoader = new AsyncMessageHandlerInvokerLoader(new StructureMapContainerProvider(new Container()));
            var invokers = invokerLoader.LoadMessageHandlerInvokers(TypeSource.FromAssembly(this)).ToList();

            var fakeHandlerInvoker = invokers.SingleOrDefault(x => x.MessageHandlerType == typeof(FakeHandler));
            fakeHandlerInvoker.ShouldNotBeNull();
            fakeHandlerInvoker.ShouldBeOfType<AsyncMessageHandlerInvoker>();
        }

        public class FakeHandler : IAsyncMessageHandler<FakeMessage>
        {
            public Task Handle(FakeMessage message)
            {
                throw new NotImplementedException();
            }
        }

        public class FakeMessage : IMessage
        {

        }
    }
}
