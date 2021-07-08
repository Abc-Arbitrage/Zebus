using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Abc.Zebus.Core;
using Abc.Zebus.Directory;
using Abc.Zebus.Persistence;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Testing.Transport;
using Abc.Zebus.Tests.Messages;
using Abc.Zebus.Transport;
using Abc.Zebus.Util;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Persistence
{
    [TestFixture]
    public abstract class PersistentTransportFixture
    {
        protected readonly Peer Self = new Peer(new PeerId("Abc.Testing.Self"), "tcp://abctest:123");
        protected readonly Peer PersistencePeer = new Peer(new PeerId("Abc.Zebus.PersistenceService.0"), "tcp://abcpersistence:123");
        protected readonly Peer AnotherPersistentPeer = new Peer(new PeerId("Another.Peer"), "tcp://anotherpeer:123", true);
        protected readonly Peer AnotherNonPersistentPeer = new Peer(new PeerId("Non.Persistent.Peer"), "tcp://nonpersistentpeer:123", true);

        protected PersistentTransport Transport { get; private set; }
        protected TestTransport InnerTransport { get; private set; }
        protected ConcurrentQueue<TransportMessage> MessagesForwardedToBus { get; private set; }
        protected StartMessageReplayCommand StartMessageReplayCommand { get; private set; }
        protected IEnumerable<Peer> StartMessageReplayCommandTargets { get; private set; }
        protected Mock<IPeerDirectory> PeerDirectory { get; private set; }


        protected abstract bool IsPersistent { get; }

        [SetUp]
        public void Setup()
        {
            InnerTransport = new TestTransport(Self.EndPoint);

            var configuration = new BusConfiguration("tcp://zebus-directory:123")
            {
                IsPersistent = IsPersistent,
                StartReplayTimeout = 60.Minutes(),
            };

            PeerDirectory = new Mock<IPeerDirectory>();
            PeerDirectory.Setup(dir => dir.GetPeersHandlingMessage(MessageBinding.Default<PersistMessageCommand>())).Returns(new[] { PersistencePeer });
            PeerDirectory.Setup(dir => dir.GetPeersHandlingMessage(It.IsAny<StartMessageReplayCommand>())).Returns(new[] { PersistencePeer });
            PeerDirectory.Setup(dir => dir.GetPeersHandlingMessage(It.IsAny<PersistMessageCommand>())).Returns(new[] { PersistencePeer });
            PeerDirectory.Setup(dir => dir.GetPeersHandlingMessage(It.IsAny<MessageHandled>())).Returns(new[] { PersistencePeer });
            PeerDirectory.Setup(dir => dir.IsPersistent(AnotherPersistentPeer.Id)).Returns(true);
            PeerDirectory.Setup(dir => dir.IsPersistent(AnotherNonPersistentPeer.Id)).Returns(false);

            Transport = new PersistentTransport(configuration, InnerTransport, PeerDirectory.Object, new DefaultMessageSendingStrategy());
            Transport.Configure(Self.Id, "test");

            MessagesForwardedToBus = new ConcurrentQueue<TransportMessage>();
            Transport.MessageReceived += MessagesForwardedToBus.Enqueue;

            Transport.OnRegistered();
            var startMessageReplayMessage = InnerTransport.Messages.FirstOrDefault(x => x.TransportMessage.MessageTypeId == MessageUtil.TypeId<StartMessageReplayCommand>());
            if (startMessageReplayMessage != null)
            {
                StartMessageReplayCommand = (StartMessageReplayCommand)startMessageReplayMessage.TransportMessage.ToMessage();
                StartMessageReplayCommandTargets = startMessageReplayMessage.Targets;
            }

            InnerTransport.Messages.Clear();
        }

        [Test]
        public void should_start_inner_transport()
        {
            Transport.Start();

            InnerTransport.IsStarted.ShouldBeTrue();
        }

        [Test]
        public void should_stop_inner_transport()
        {
            Transport.Start();
            Transport.Stop();

            InnerTransport.IsStopped.ShouldBeTrue();
        }

        [Test]
        public void should_prioritize_infrastructure_messages()
        {
            Transport.Start();

            Transport.MessageReceived += x =>
            {
                Thread.Sleep(200);
            };

            InnerTransport.RaiseMessageReceived(new FakeCommand(1).ToTransportMessage());
            InnerTransport.RaiseMessageReceived(new FakeInfrastructureCommand().ToTransportMessage());
            Thread.Sleep(20);

            MessagesForwardedToBus.ShouldContain(x => x.MessageTypeId == new MessageTypeId(typeof(FakeInfrastructureCommand)));

            Transport.Stop();
        }

        [TestCase(PeerUpdateAction.Started)]
        [TestCase(PeerUpdateAction.Updated)]
        public void should_proxy_peer_updated_to_inner_transport(PeerUpdateAction updateAction)
        {
            Transport.OnPeerUpdated(AnotherNonPersistentPeer.Id, updateAction);

            var updatedPeer = InnerTransport.UpdatedPeers.ExpectedSingle();
            updatedPeer.PeerId.ShouldEqual(AnotherNonPersistentPeer.Id);
            updatedPeer.UpdateAction.ShouldEqual(updateAction);
        }
    }
}
