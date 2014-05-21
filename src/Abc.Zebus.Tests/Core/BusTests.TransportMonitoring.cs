using Abc.Zebus.Core;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Monitoring;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Transport;
using Abc.Zebus.Transport;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Core
{
    public partial class BusTests
    {
        [Test]
        public void should_publish_SocketConnected_event()
        {
            _bus.Start();
            SetupPeersHandlingMessage<SocketConnected>(_peerUp);

            var remotePeerId = new PeerId("peer");
            var expected = new SocketConnected(_self.Id, remotePeerId, "endpoint");

            using (MessageId.PauseIdGeneration())
            {
                _transport.RaiseSocketConnected(remotePeerId, "endpoint");

                _transport.ExpectExactly(new TransportMessageSent(expected.ToTransportMessage(_self), _peerUp));
            }
        }

        [Test]
        public void should_publish_SocketDisconnected_event()
        {
            _bus.Start();
            SetupPeersHandlingMessage<SocketDisconnected>(_peerUp);
            var remotePeerId = new PeerId("peer");

            using (MessageId.PauseIdGeneration())
            {
                _transport.RaiseSocketDisconnected(remotePeerId, "endpoint");

                var expected = new SocketDisconnected(_self.Id, remotePeerId, "endpoint");
                _transport.ExpectExactly(new TransportMessageSent(expected.ToTransportMessage(_self), _peerUp));
            }
        }

        [Test]
        public void should_not_publish_SocketDisconnected_when_stopping_peer()
        {
            SetupPeersHandlingMessage<SocketDisconnected>(_peerUp);

            var remotePeerId = new PeerId("peer");
            var bus = new Bus(_transport, _directoryMock.Object, _messageSerializer, _messageDispatcherMock.Object, new PublishSocketDisconnectedStoppingStrategy(remotePeerId, "endpoint"));
            bus.Configure(_self.Id, "test");
            bus.Start();
            
            bus.Stop();

           _transport.ExpectNothing();
        }

        public class PublishSocketDisconnectedStoppingStrategy : IStoppingStrategy
        {
            private readonly PeerId _remotePeerId;
            private readonly string _endpoint;

            public PublishSocketDisconnectedStoppingStrategy(PeerId remotePeerId, string endpoint)
            {
                _remotePeerId = remotePeerId;
                _endpoint = endpoint;
            }

            public void Stop(ITransport transport, IMessageDispatcher messageDispatcher)
            {
                ((TestTransport)transport).RaiseSocketDisconnected(_remotePeerId, _endpoint);
            }
        }
    }
}