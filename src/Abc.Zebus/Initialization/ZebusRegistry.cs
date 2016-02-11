using Abc.Zebus.Core;
using Abc.Zebus.Directory;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Dispatch.Pipes;
using Abc.Zebus.Monitoring;
using Abc.Zebus.Persistence;
using Abc.Zebus.Scan;
using Abc.Zebus.Serialization;
using Abc.Zebus.Transport;
using StructureMap.Configuration.DSL;

namespace Abc.Zebus.Initialization
{
    public class ZebusRegistry : Registry
    {
        public ZebusRegistry()
        {
            ForSingletonOf<IMessageDispatcher>().Use<MessageDispatcher>();
            Forward<IMessageDispatcher, IProvideQueueLength>();

            ForSingletonOf<IDispatcherTaskSchedulerFactory>().Use<DispatcherTaskSchedulerFactory>();
            For<IMessageSerializer>().Use<MessageSerializer>();

            ForSingletonOf<IZmqSocketOptions>().Use<ZmqSocketOptions>();
            ForSingletonOf<IPersistentTransport>().Use<PersistentTransport>().Ctor<ITransport>().Is<ZmqTransport>();
            Forward<IPersistentTransport, ITransport>();

            ForSingletonOf<IBus>().Use<Bus>();
            Forward<IBus, IMessageDispatchFactory>();

            ForSingletonOf<IPipeManager>().Use<PipeManager>();

            ForSingletonOf<PeerDirectoryClient>().Use<PeerDirectoryClient>();
            Forward<PeerDirectoryClient, IPeerDirectory>();

            For<IMessageHandlerInvokerLoader>().Add(ctx => ctx.GetInstance<SyncMessageHandlerInvokerLoader>());
            For<IMessageHandlerInvokerLoader>().Add<AsyncMessageHandlerInvokerLoader>();

            ForSingletonOf<IMessageSendingStrategy>().Use<DefaultMessageSendingStrategy>();
            ForSingletonOf<IStoppingStrategy>().Use<DefaultStoppingStrategy>();
        }
    }
}
