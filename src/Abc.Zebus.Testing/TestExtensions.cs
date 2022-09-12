using System;
using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Core;
using Abc.Zebus.Directory;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Persistence;
using Abc.Zebus.Serialization;
using Abc.Zebus.Testing.Directory;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Testing.Transport;
using Abc.Zebus.Transport;
using Abc.Zebus.Util;
using Moq;

namespace Abc.Zebus.Testing
{
    public static class TestExtensions
    {
        public static TransportMessage ToTransportMessage(this IMessage message, Peer? sender = null, bool? wasPersisted = null)
        {
            sender ??= new Peer(new PeerId("Abc.Testing.Peer"), "tcp://abctest:159");

            var serializer = new MessageSerializer();
            var content = serializer.Serialize(message);

            return new TransportMessage(message.TypeId(), content, sender) { WasPersisted = wasPersisted };
        }

        public static TransportMessage ToPersistTransportMessage(this TransportMessage transportMessage, params PeerId[] peerIds)
        {
            return transportMessage.ConvertToPersistTransportMessage(peerIds.ToList());
        }

        public static IMessage? ToMessage(this TransportMessage transportMessage)
        {
            var serializer = new MessageSerializer();
            return serializer.ToMessage(transportMessage);
        }

        public static TransportMessage ToReplayedTransportMessage(this TransportMessage message, Guid replayId)
        {
            return new MessageReplayed(replayId, message).ToTransportMessage();
        }

        public static PeerDescriptor ToPeerDescriptor(this Peer peer, bool isPersistent, IEnumerable<Subscription> subscriptions)
        {
            return new PeerDescriptor(peer.Id, peer.EndPoint, isPersistent, peer.IsUp, peer.IsResponding, SystemDateTime.UtcNow, subscriptions.ToArray());
        }

        public static PeerDescriptor ToPeerDescriptor(this Peer peer, bool isPersistent, params MessageTypeId[] messageTypeIds)
        {
            return peer.ToPeerDescriptor(isPersistent, messageTypeIds.Select(x => new Subscription(x)));
        }

        public static PeerDescriptor ToPeerDescriptor(this Peer peer, bool isPersistent, params Type[] messageTypes)
        {
            return peer.ToPeerDescriptor(isPersistent, messageTypes.Select(x => new Subscription(MessageUtil.GetTypeId(x))));
        }

        public static PeerDescriptor ToPeerDescriptorWithRoundedTime(this Peer peer, bool isPersistent, params Type[] messageTypes)
        {
            var descriptor = peer.ToPeerDescriptor(isPersistent, messageTypes.Select(x => new Subscription(MessageUtil.GetTypeId(x))));
            descriptor.TimestampUtc = SystemDateTime.UtcNow.RoundToMillisecond();
            return descriptor;
        }

        public static PeerDescriptor ToPeerDescriptor(this Peer peer, bool isPersistent, params string[] messageTypes)
        {
            return peer.ToPeerDescriptor(isPersistent, messageTypes.Select(x => new Subscription(new MessageTypeId(x))));
        }

        public static PeerDescriptor ToPeerDescriptor(this Peer peer, bool isPersistent)
        {
            return peer.ToPeerDescriptor(isPersistent, Enumerable.Empty<Subscription>());
        }

        public static IBus CreateAndStartInMemoryBus(this BusFactory busFactory)
        {
            return busFactory.CreateAndStartInMemoryBus(new TestPeerDirectory(), new TestTransport());
        }

        public static IBus CreateAndStartInMemoryBus(this BusFactory busFactory, TestPeerDirectory directory, ITransport transport)
        {
            return busFactory.WithConfiguration("in-memory-bus", "Memory")
                             .ConfigureContainer(cfg =>
                             {
                                 cfg.ForSingletonOf<IPeerDirectory>().Use(directory);
                                 cfg.ForSingletonOf<ITransport>().Use(transport);
                             }).CreateAndStartBus();
        }

        public static IMessageHandlerInvocation ToInvocation(this IMessage message, MessageContext context)
        {
            var invocationMock = new Mock<IMessageHandlerInvocation>();
            invocationMock.SetupGet(x => x.Context).Returns(context);
            invocationMock.SetupGet(x => x.Messages).Returns(new List<IMessage> { message });
            invocationMock.Setup(x => x.SetupForInvocation()).Returns(() => MessageContext.SetCurrent(context));
            invocationMock.Setup(x => x.SetupForInvocation(It.IsAny<object>())).Returns(() => MessageContext.SetCurrent(context));

            return invocationMock.Object;
        }

        public static IMessageHandlerInvocation ToInvocation(this IMessage message)
        {
            return ToInvocation(message, MessageContext.CreateTest("u.name"));
        }

        public static IEnumerable<Subscription> OfMessageType<T>(this IEnumerable<Subscription> subscriptions) where T : IMessage
        {
            return subscriptions.Where(x => x.MessageTypeId == MessageUtil.TypeId<T>());
        }

        public static Peer GetPeer(this ZmqTransport transport)
        {
            return new Peer(transport.PeerId, transport.InboundEndPoint);
        }
    }
}
