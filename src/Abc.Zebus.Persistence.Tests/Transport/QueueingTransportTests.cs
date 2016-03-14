using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Directory;
using Abc.Zebus.Persistence.Transport;
using Abc.Zebus.Persistence.Util;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Testing.Transport;
using Abc.Zebus.Transport;
using Moq;
using NUnit.Framework;
using ProtoBuf;

namespace Abc.Zebus.Persistence.Tests.Transport
{
    [TestFixture]
    public class QueueingTransportTests
    {
        private TestTransport _innerTransport;
        private QueueingTransport _transport;
        private Peer _targetPeer;
        private List<PeerDescriptor> _allPeers;
        private readonly Mock<IPersistenceConfiguration> _configurationMock = new Mock<IPersistenceConfiguration>();

        [SetUp]
        public void Setup()
        {
            _innerTransport = new TestTransport("tcp://abctest:888");

            var peerDirectoryMock = new Mock<IPeerDirectory>();
            _allPeers = new List<PeerDescriptor>
            {
                new PeerDescriptor(new PeerId("Abc.Testing.1"), "endpoint1", true, true, true, SystemDateTime.UtcNow),
                new PeerDescriptor(new PeerId("Abc.Testing.2"), "endpoint2", false, true, true, SystemDateTime.UtcNow),
            };
            peerDirectoryMock.Setup(dir => dir.GetPeerDescriptors())
                             .Returns(_allPeers);
            
            _configurationMock.SetupGet(conf => conf.QueuingTransportStopTimeout).Returns(100.Milliseconds());
            _transport = new QueueingTransport(_innerTransport, peerDirectoryMock.Object, _configurationMock.Object);

            _targetPeer = new Peer(new PeerId("Abc.Testing.Target"), "tcp://abctest:999");
        }

        [Test]
        public void should_proxy_configure()
        {
            var peerId = new PeerId("Abc.Testing.0");
            _transport.Configure(peerId, "test");

            _innerTransport.PeerId.ShouldEqual(peerId);
            _innerTransport.IsConfigured.ShouldBeTrue();
        }

        [Test]
        public void should_proxy_start()
        {
            _transport.Start();
            _innerTransport.IsStarted.ShouldBeTrue();
        }

        [Test]
        public void should_proxy_stop()
        {
            _transport.Stop();
            _innerTransport.IsStopped.ShouldBeTrue();
        }

        [Test]
        public void should_proxy_send()
        {
            var transportMessage = new TestCommand().ToTransportMessage();
            var targets = new[] { _targetPeer };
            var expectedSendContext = new SendContext();
            _transport.Send(transportMessage, targets, expectedSendContext);

            var sentMessage = _innerTransport.Messages.Single();
            sentMessage.TransportMessage.ShouldEqual(transportMessage);
            sentMessage.Targets.ShouldBeEquivalentTo(targets);
            sentMessage.Context.ShouldEqual(expectedSendContext);
        }

        [Test]
        public void should_proxy_peer_updated()
        {
            _transport.OnPeerUpdated(_targetPeer.Id, PeerUpdateAction.Stopped);

            _innerTransport.UpdatedPeers.ShouldBeEquivalentTo(new[] { new UpdatedPeer(_targetPeer.Id, PeerUpdateAction.Stopped) });
        }

        [Test]
        public void should_proxy_registered()
        {
            _transport.OnRegistered();

            _innerTransport.IsRegistered.ShouldBeTrue();
        }

        [Test]
        public void should_queue_receives()
        {
            _transport.Start();

            var receiveSignal = new ManualResetEvent(false);
            var receivedMessagesCount = 0;

            _transport.MessageReceived += x =>
            {
                receiveSignal.WaitOne();
                ++receivedMessagesCount;
            };

            for (var i = 0; i < 10; ++i)
            {
                _innerTransport.RaiseMessageReceived(new TestCommand().ToTransportMessage());
            }

            Wait.Until(() => _transport.PendingReceiveCount >= 9, 500.Milliseconds(), "Receives are not queued");

            receiveSignal.Set();

            Wait.Until(() => receivedMessagesCount == 10, 500.Milliseconds(), "Receives are not processed");

            _transport.PendingReceiveCount.ShouldEqual(0);

            _transport.Stop();
        }

        [Test]
        public void should_process_infrastructure_messages_synchronously()
        {
            _transport.Start();

            var receiveSignal = new ManualResetEvent(false);
            var infrastructureMessageReceived = false;

            Action<TransportMessage> onMessageReceived = x =>
            {
                if (x.MessageTypeId.IsInfrastructure())
                    infrastructureMessageReceived = true;
                else
                    receiveSignal.WaitOne();
            };

            _transport.MessageReceived += onMessageReceived;

            _innerTransport.RaiseMessageReceived(new TestCommand().ToTransportMessage());
            _innerTransport.RaiseMessageReceived(new PingPeerCommand().ToTransportMessage());

            infrastructureMessageReceived.ShouldBeTrue();

            receiveSignal.Set();
        }

        [Test]
        public void should_send_PersistenceStopping_Message_and_wait_for_acks_before_stopping()
        {
            _configurationMock.Setup(conf => conf.QueuingTransportStopTimeout).Returns(15.Seconds());

            using (MessageId.PauseIdGeneration())
            {
                var self = new Peer(new PeerId("Abc.Self.0"), _innerTransport.InboundEndPoint);
                _allPeers.Add(new PeerDescriptor(self.Id, self.EndPoint, false, true, true, SystemDateTime.UtcNow));
                _transport.Configure(self.Id, "test");
                _transport.Start();

                var stopped = false;
                Task.Factory.StartNew(() =>
                {
                    _transport.Stop();
                    stopped = true;
                });

                Wait.Until(() => _innerTransport.Messages.Count == 1, 2.Seconds());
                stopped.ShouldBeFalse();

                var targets = _allPeers.Where(peer => peer.PeerId != self.Id).Select(desc => desc.Peer).ToArray(); // do not send to self
                _innerTransport.ExpectExactly(new TransportMessageSent(new TransportMessage(MessageTypeId.PersistenceStopping, new byte[0], self), targets));

                _innerTransport.RaiseMessageReceived(new TransportMessage(MessageTypeId.PersistenceStoppingAck, new byte[0], _allPeers[0].Peer));
                _innerTransport.RaiseMessageReceived(new TransportMessage(MessageTypeId.PersistenceStoppingAck, new byte[0], _allPeers[1].Peer));

                Wait.Until(() => stopped, 2.Seconds());
            }
        }

        [Test]
        public void should_timeout_on_shutdown_if_peers_dont_answer()
        {
            _configurationMock.Setup(conf => conf.QueuingTransportStopTimeout).Returns(100.Millisecond());

            using (MessageId.PauseIdGeneration())
            {
                var self = new Peer(new PeerId("Abc.Self.0"), _innerTransport.InboundEndPoint);
                _transport.Configure(self.Id, "test");
                _transport.Start();

                var stopped = false;
                Task.Factory.StartNew(() =>
                {
                    _transport.Stop();
                    stopped = true;
                });

                Wait.Until(() => _innerTransport.Messages.Count == 1, 2.Seconds());
                stopped.ShouldBeFalse();

                _innerTransport.RaiseMessageReceived(new TransportMessage(MessageTypeId.PersistenceStoppingAck, new byte[0], _allPeers[1].Peer));

                Wait.Until(() => stopped, 2.Seconds());
            }
        }

        [ProtoContract]
        private class TestCommand : ICommand
        {
        }
    }
}