using Abc.Zebus.Core;
using Abc.Zebus.DependencyInjection;
using Abc.Zebus.Directory;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Dispatch.Pipes;
using Abc.Zebus.Monitoring;
using Abc.Zebus.Persistence;
using Abc.Zebus.Scan;
using Abc.Zebus.Serialization;
using Abc.Zebus.Transport;
using Lamar;

namespace Abc.Zebus.Initialization
{
    public class LamarZebusRegistry : ServiceRegistry
    {
        public LamarZebusRegistry(IContainer container)
        {
            ForSingletonOf<IDependencyInjectionContainerProvider>().Use(x => new LamarContainerProvider(container));
            ForSingletonOf<IMessageDispatcher>().Use<MessageDispatcher>();
            ForSingletonOf<IProvideQueueLength>().Use(x => (IProvideQueueLength)x.GetInstance<IMessageDispatcher>());

            ForSingletonOf<IDispatchQueueFactory>().Use<DispatchQueueFactory>();
            For<IMessageSerializer>().Use<MessageSerializer>();

            ForSingletonOf<IPersistentTransport>().Use<PersistentTransport>().Ctor<ITransport>().Is<ZmqTransport>();
            ForSingletonOf<ITransport>().Use(x => x.GetInstance<IPersistentTransport>());

            Injectable<MessageContext>();
            ForSingletonOf<IBus>().Use<Bus>();
            ForSingletonOf<IMessageDispatchFactory>().Use(x => (IMessageDispatchFactory)x.GetInstance<IBus>());

            ForSingletonOf<IPipeManager>().Use<PipeManager>();

            ForSingletonOf<PeerDirectoryClient>().Use<PeerDirectoryClient>();
            ForSingletonOf<IPeerDirectory>().Use(x => x.GetInstance<PeerDirectoryClient>());

            For<IMessageHandlerInvokerLoader>().Add<SyncMessageHandlerInvokerLoader>();
            For<IMessageHandlerInvokerLoader>().Add<AsyncMessageHandlerInvokerLoader>();
            For<IMessageHandlerInvokerLoader>().Add<BatchedMessageHandlerInvokerLoader>();

            ForSingletonOf<IMessageSendingStrategy>().Use<DefaultMessageSendingStrategy>();
            ForSingletonOf<IStoppingStrategy>().Use<DefaultStoppingStrategy>();

            ForSingletonOf<IZmqOutboundSocketErrorHandler>().Use<DefaultZmqOutboundSocketErrorHandler>();
        }
    }
}
