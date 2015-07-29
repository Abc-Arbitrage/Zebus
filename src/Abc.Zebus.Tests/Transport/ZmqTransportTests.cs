using Abc.Zebus.Directory;
using Abc.Zebus.Serialization;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Messages;
using Abc.Zebus.Transport;
using Abc.Zebus.Util;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Abc.Zebus.Tests.Transport
{
    [TestFixture]
    public class ZmqTransportTests
    {
        private const string _environment = "Test";
        private List<ZmqTransport> _transports;

        [SetUp]
        public void Setup()
        {
            _transports = new List<ZmqTransport>();
        }

        [TearDown]
        public void Teardown()
        {
            foreach (var transport in _transports)
            {
                try
                {
                    if (transport.IsListening)
                        transport.Stop();
                }
                catch (Exception)
                {
                }
            }
        }

        [Test]
        public void should_not_crash_when_stopping_if_it_was_not_started()
        {
            var configurationMock = new Mock<IZmqTransportConfiguration>();
            configurationMock.SetupGet(x => x.WaitForEndOfStreamAckTimeout).Returns(100.Milliseconds());
            var transport = new ZmqTransport(configurationMock.Object, new ZmqSocketOptions());

            Assert.That(transport.Stop, Throws.Nothing);
        }

        [Test]
        public void should_not_filter_received_messages_when_environment_is_not_specified()
        {
            var transport1 = CreateAndStartZmqTransport(environment: null);

            var transport2ReceivedMessages = new ConcurrentBag<TransportMessage>();
            var transport2 = CreateAndStartZmqTransport(onMessageReceived: transport2ReceivedMessages.Add, environment: "NotTest");
            var transport2Peer = new Peer(new PeerId("Abc.Testing.2"), transport2.InboundEndPoint);

            var message = new FakeCommand(1).ToTransportMessage();
            transport1.Send(message, new[] { transport2Peer });

            Wait.Until(() => transport2ReceivedMessages.Count >= 1, 500.Milliseconds());
            transport2ReceivedMessages.Single().Id.ShouldEqual(message.Id);
        }

        [Test]
        public void should_not_let_the_outbound_thread_die_if_a_peer_cannot_be_resolved()
        {
            var senderTransport = CreateAndStartZmqTransport(environment: null);

            var receivedMessages = new ConcurrentBag<TransportMessage>();
            var destinationTransport = CreateAndStartZmqTransport(onMessageReceived: receivedMessages.Add, environment: "NotTest");
            var destinationPeer = new Peer(new PeerId("Abc.Testing.2"), destinationTransport.InboundEndPoint);
            var nonExistingPeer = new Peer(new PeerId("Abc.NonExistingPeer.2"), "tcp://non_existing_peer:1234");

            var message = new FakeCommand(1).ToTransportMessage();
            senderTransport.Send(message, new[] { nonExistingPeer });
            senderTransport.Send(message, new[] { destinationPeer });

            Wait.Until(() => receivedMessages.Count >= 1, 1000.Milliseconds(), "The outbound thread was killed and couldn't connect to the next peer");
        }

        [Test]
        public void should_not_dispatch_messages_received_from_wrong_environment()
        {
            var transport1ReceivedMessages = new ConcurrentBag<TransportMessage>();
            var transport1 = CreateAndStartZmqTransport(onMessageReceived: transport1ReceivedMessages.Add);

            var transport2ReceivedMessages = new ConcurrentBag<TransportMessage>();
            var transport2 = CreateAndStartZmqTransport(onMessageReceived: transport2ReceivedMessages.Add, environment: "NotTest");
            var transport2Peer = new Peer(new PeerId("Abc.Testing.2"), transport2.InboundEndPoint);

            var message1 = new FakeCommand(1).ToTransportMessage();
            var message2 = new FakeCommand(2).ToTransportMessage();
            transport1.Send(message1, new[] { transport2Peer }); // should not arrive

            Thread.Sleep(500); //:(
            transport2.Configure(transport2Peer.Id, _environment);
            transport1.Send(message2, new[] { transport2Peer }); //should arrive

            Wait.Until(() => transport2ReceivedMessages.Count >= 1, 500.Milliseconds());
            transport2ReceivedMessages.Single().Id.ShouldEqual(message2.Id);
        }

        [Test, Timeout(5000)]
        public void should_send_messages()
        {
            var transport1ReceivedMessages = new ConcurrentBag<TransportMessage>();
            var transport1 = CreateAndStartZmqTransport(onMessageReceived: transport1ReceivedMessages.Add);
            var transport1Peer = new Peer(new PeerId("Abc.Testing.1"), transport1.InboundEndPoint);

            var transport2ReceivedMessages = new ConcurrentBag<TransportMessage>();
            var transport2 = CreateAndStartZmqTransport(onMessageReceived: transport2ReceivedMessages.Add);
            var transport2Peer = new Peer(new PeerId("Abc.Testing.2"), transport2.InboundEndPoint);

            var message1 = new FakeCommand(1).ToTransportMessage();
            transport1.Send(message1, new[] { transport2Peer });

            Wait.Until(() => transport2ReceivedMessages.Count == 1, 500.Milliseconds());

            var message2 = new FakeCommand(2).ToTransportMessage();
            transport2.Send(message2, new[] { transport1Peer });

            Wait.Until(() => transport1ReceivedMessages.Count == 1, 500.Milliseconds());
        }

        [Test]
        public void should_support_peer_endpoint_modifications()
        {
            var senderTransport = CreateAndStartZmqTransport();

            var receivedMessages = new ConcurrentBag<TransportMessage>();
            var receiverTransport = CreateAndStartZmqTransport(onMessageReceived: receivedMessages.Add);

            var receiver = new Peer(new PeerId("Abc.Testing.Receiver.0"), receiverTransport.InboundEndPoint);

            senderTransport.Send(new FakeCommand(0).ToTransportMessage(), new[] { receiver });
            Wait.Until(() => receivedMessages.Count == 1, 500.Milliseconds());

            var newEndPoint = "tcp://127.0.0.1:" + TcpUtil.GetRandomUnusedPort();
            receiverTransport.Stop();
            receiverTransport = CreateAndStartZmqTransport(newEndPoint, receivedMessages.Add);
            receiver.EndPoint = receiverTransport.InboundEndPoint;

            senderTransport.Send(new FakeCommand(0).ToTransportMessage(), new[] { receiver });
            Wait.Until(() => receivedMessages.Count == 2, 500.Milliseconds(), "unable to receive message");
        }
       
        [Test, Repeat(10)]
        public void should_not_reuse_a_port_used_in_another_envionment()
        {
            const string peerId = "Abc.Peer.0";

            var doNotUsePortFilePath = PathUtil.InBaseDirectory(peerId + ".inboundport.secondenv");
            var expectedPort = TcpUtil.GetRandomUnusedPort() + 5; // scientifical method to determine what port will be used by the transport :P

            File.WriteAllText(doNotUsePortFilePath, expectedPort.ToString());

            var transport = CreateAndStartZmqTransport(peerId: peerId);
            var endpoint = new ZmqEndPoint(transport.InboundEndPoint);

            endpoint.GetPort().ShouldNotEqual(expectedPort);
            Console.WriteLine("{0} => {1}", endpoint.GetPort(), expectedPort);
        }

        [Test, Repeat(5)] 
        public void should_terminate_zmq_connection_of_a_forgotten_peer_after_some_time()
        {
            var senderTransport = CreateAndStartZmqTransport();
            var receiverTransport = CreateAndStartZmqTransport();
            var receiverPeer = new Peer(new PeerId("Abc.Testing.2"), receiverTransport.InboundEndPoint);

            var message = new FakeCommand(1).ToTransportMessage();
            senderTransport.Send(message, new[] { receiverPeer });
            Wait.Until(() => senderTransport.OutboundSocketCount == 1, 500.Milliseconds());

            senderTransport.OnPeerUpdated(receiverPeer.Id, PeerUpdateAction.Decommissioned);

            Thread.Sleep(100);

            senderTransport.OutboundSocketCount.ShouldEqual(1);

            using (SystemDateTime.Set(utcNow: SystemDateTime.UtcNow.Add(30.Seconds())))
            {
                Wait.Until(() => senderTransport.OutboundSocketCount == 0, 1.Seconds(), "Socket should be disconnected");
            }
        }

        [Test, Repeat(5)]
        public void should_terminate_zmq_connection_of_a_started_peer_with_no_delay()
        {
            var senderTransport = CreateAndStartZmqTransport();
            var receiverTransport = CreateAndStartZmqTransport();
            var receiverPeer = new Peer(new PeerId("Abc.Testing.2"), receiverTransport.InboundEndPoint);

            var message = new FakeCommand(1).ToTransportMessage();
            senderTransport.Send(message, new[] { receiverPeer });
            Wait.Until(() => senderTransport.OutboundSocketCount == 1, 500.Milliseconds());

            senderTransport.OnPeerUpdated(receiverPeer.Id, PeerUpdateAction.Started);

            Wait.Until(() => senderTransport.OutboundSocketCount == 0, 300.Milliseconds(), "Socket should be disconnected");
        }

        [Test]
        public void should_receive_many_messages()
        {
            var senderTransport = CreateAndStartZmqTransport();

            var receviedMessages = new List<TransportMessage>();
            var receiverTransport = CreateAndStartZmqTransport(onMessageReceived: receviedMessages.Add);
            var receiver = new Peer(new PeerId("Abc.Testing.Receiver.Up"), receiverTransport.InboundEndPoint);

            for (var i = 0; i < 10; ++i)
            {
                var message = new FakeCommand(i).ToTransportMessage();
                senderTransport.Send(message, new[] { receiver });
            }

            Wait.Until(() => receviedMessages.Count == 10, 1.Second());

            for (var i = 0; i < 10; ++i)
            {
                var message = (FakeCommand)receviedMessages[i].ToMessage();
                message.FakeId.ShouldEqual(i);
            }
        }

        [Test, Timeout(5000)]
        public void should_not_block_when_hitting_high_water_mark()
        {
            var senderTransport = CreateAndStartZmqTransport();
            senderTransport.SocketOptions.SendHighWaterMark = 3;
            senderTransport.SocketOptions.SendTimeout = 50.Milliseconds();
            senderTransport.SocketOptions.SendRetriesBeforeSwitchingToClosedState = 2;

            var receviedMessages = new List<TransportMessage>();
            var upReceiverTransport = CreateAndStartZmqTransport(onMessageReceived: receviedMessages.Add);
            var upReceiver = new Peer(new PeerId("Abc.Testing.Receiver.Up"), upReceiverTransport.InboundEndPoint);

            var downReceiverTransport = CreateAndStartZmqTransport();
            var downReceiver = new Peer(new PeerId("Abc.Testing.Receiver.Down"), downReceiverTransport.InboundEndPoint);

            downReceiverTransport.Stop();

            for (var i = 1; i <= 10; ++i)
            {
                var message = new FakeCommand(i).ToTransportMessage();
                senderTransport.Send(message, new[] { upReceiver, downReceiver });

                var expectedMessageCount = i;
                Wait.Until(() => receviedMessages.Count == expectedMessageCount, 500.Milliseconds(), "Failed to send message after " + i + " successful sent");
            }
        }

        [Test, Timeout(5000)]
        public void should_not_wait_blocked_peers_on_every_send()
        {
            var senderTransport = CreateAndStartZmqTransport();
            senderTransport.SocketOptions.SendHighWaterMark = 1;
            senderTransport.SocketOptions.SendTimeout = 50.Milliseconds();
            senderTransport.SocketOptions.SendRetriesBeforeSwitchingToClosedState = 0;

            var receivedMessages = new List<TransportMessage>();
            var upReceiverTransport = CreateAndStartZmqTransport(onMessageReceived: receivedMessages.Add);
            var upReceiver = new Peer(new PeerId("Abc.Testing.Receiver.Up"), upReceiverTransport.InboundEndPoint);

            var downReceiverTransport = CreateAndStartZmqTransport();
            var downReceiver = new Peer(new PeerId("Abc.Testing.Receiver.Down"), downReceiverTransport.InboundEndPoint);

            Console.WriteLine("Stopping receiver");

            downReceiverTransport.Stop();

            Console.WriteLine("Receiver stopped");

            for (var i = 1; i <= 10; ++i)
            {
                var senderStopwatch = Stopwatch.StartNew();
                var message = new FakeCommand(i).ToTransportMessage();
                senderTransport.Send(message, new[] { upReceiver, downReceiver });
                Console.WriteLine("Send a message to two peers in " + senderStopwatch.Elapsed);
            }

            var receiverStopwatch = Stopwatch.StartNew();
            Wait.Until(() => receivedMessages.Count == 10, 2.Seconds(), "Timed out while waiting for messages");
            receiverStopwatch.Stop();
            Console.WriteLine("Elapsed time to get messages: " + receiverStopwatch.Elapsed);
            receiverStopwatch.ElapsedMilliseconds.ShouldBeLessOrEqualThan(200, "Throughput is too low");
        }

        [Test]
        public void should_send_various_sized_messages()
        {
            var senderTransport = CreateAndStartZmqTransport();
            senderTransport.SocketOptions.SendHighWaterMark = 3;

            var receviedMessages = new List<TransportMessage>();
            var receiverTransport = CreateAndStartZmqTransport(onMessageReceived: receviedMessages.Add);
            var receiver = new Peer(new PeerId("Abc.Testing.Receiver.Up"), receiverTransport.InboundEndPoint);

            var messageBytes = new byte[5000];
            new Random().NextBytes(messageBytes);

            var bigMessage = new TransportMessage(new MessageTypeId(typeof(FakeCommand)), messageBytes, new PeerId("X"), senderTransport.InboundEndPoint, MessageId.NextId());
            senderTransport.Send(bigMessage, new[] { receiver });

            Wait.Until(() => receviedMessages.Count == 1, 150.Milliseconds());

            receviedMessages[0].ShouldHaveSamePropertiesAs(bigMessage);

            var smallMessage = new TransportMessage(new MessageTypeId(typeof(FakeCommand)), new byte[1], new PeerId("X"), senderTransport.InboundEndPoint, MessageId.NextId());
            senderTransport.Send(smallMessage, new[] { receiver });

            Wait.Until(() => receviedMessages.Count == 2, 150.Milliseconds());

            receviedMessages[1].ShouldHaveSamePropertiesAs(smallMessage);
        }

        [Test]
        public void should_send_message_to_self()
        {
            var receviedMessages = new List<TransportMessage>();
            var transport = CreateAndStartZmqTransport(onMessageReceived: receviedMessages.Add);
            var self = new Peer(new PeerId("Abc.Testing.0"), transport.InboundEndPoint);

            transport.Send(new FakeCommand(1).ToTransportMessage(), new[] { self });

            Wait.Until(() => receviedMessages.Count == 1, 500.Milliseconds());
        }

        [Test]
        public void should_not_forward_messages_to_upper_layer_when_stopping()
        {
            var receivedMessages = new List<TransportMessage>();

            var receivingPeerId = new PeerId("Abc.Receiving.0");
            var stopwatch = Stopwatch.StartNew();
            var receivingTransport = CreateAndStartZmqTransport(onMessageReceived: receivedMessages.Add, peerId: receivingPeerId.ToString(), 
                                                                transportFactory: conf => new CapturingIsListeningTimeZmqTransport(conf, stopwatch));
            var receivingPeer = new Peer(receivingPeerId, receivingTransport.InboundEndPoint);
            var messageSerializer = new MessageSerializer();
            bool receivedWhileNotListening = false;
            receivingTransport.MessageReceived += message =>
            {
                var cmdWithTimetamp = (FakeCommandWithTimestamp)messageSerializer.Deserialize(message.MessageTypeId, message.MessageBytes);

                if (cmdWithTimetamp.Timestamp > ((CapturingIsListeningTimeZmqTransport)receivingTransport).IsListeningSwitchTimestamp)
                    receivedWhileNotListening = true;
            };

            var sendingTransport = CreateAndStartZmqTransport();
            var shouldSendMessages = true;
            var sendTask = Task.Factory.StartNew(() =>
            {
                while (shouldSendMessages)
                    sendingTransport.Send(new FakeCommandWithTimestamp(stopwatch.Elapsed).ToTransportMessage(), new[] { receivingPeer });

            });
            Wait.Until(() => sendTask.Status == TaskStatus.Running, 10.Seconds());
            Wait.Until(() => receivedMessages.Count > 1, 10.Seconds());
            
            receivingTransport.Stop();

            receivedWhileNotListening.ShouldBeFalse();
            shouldSendMessages = false;
            sendingTransport.Stop();
        }

        [Test]
        public void should_process_all_messages_in_buffer_on_stop()
        {
            var receivedMessages = new List<TransportMessage>();

            var receivingTransport = CreateAndStartZmqTransport(onMessageReceived: receivedMessages.Add);
            var sendingTransport = CreateAndStartZmqTransport();
            var receivingPeer = new Peer(new PeerId("Abc.Receiving.0"), receivingTransport.InboundEndPoint);
            var count = 0;
            var shouldSendMessages = true;
            var senderTask = new Thread(() =>
            {
                while (shouldSendMessages)
                    sendingTransport.Send(new FakeCommand(count++).ToTransportMessage(), new[] { receivingPeer });

                sendingTransport.Stop();
            });
            senderTask.Start();
            Wait.Until(() => receivedMessages.Count != 0, 2.Seconds());

            Console.WriteLine("Stopping the sender for the end\r\n\r\n");
            shouldSendMessages = false;
            senderTask.Join();
            Console.WriteLine("Stopping the receiver for the end\r\n\r\n");
            receivingTransport.Stop();
            Console.WriteLine("Receiver stopped\r\n\r\n");

            receivedMessages.Count.ShouldEqual(count);
        }

        [Test]
        public void should_disconnect_peer_socket_of_a_stopped_peer_after_some_time()
        {
            var transport1 = CreateAndStartZmqTransport(peerId: "Abc.Testing.1");
            var peer1 = new Peer(transport1.PeerId, transport1.InboundEndPoint);

            var transport2 = CreateAndStartZmqTransport(peerId: "Abc.Testing.2");
            var peer2 = new Peer(transport2.PeerId, transport2.InboundEndPoint);

            transport1.Send(new FakeCommand(0).ToTransportMessage(), new[] { peer2 });
            transport2.Send(new FakeCommand(0).ToTransportMessage(), new[] { peer1 });
            Wait.Until(() => transport1.OutboundSocketCount == 1, 500.Milliseconds());
            Wait.Until(() => transport2.OutboundSocketCount == 1, 500.Milliseconds());
            
            transport2.Stop();

            using (SystemDateTime.Set(utcNow: SystemDateTime.UtcNow.Add(30.Seconds())))
            {
                Wait.Until(() => transport1.OutboundSocketCount == 0, 1000.Milliseconds());
            }
        }

        private ZmqTransport CreateAndStartZmqTransport(string endPoint = null, Action<TransportMessage> onMessageReceived = null, string peerId = "The.Peer",
                                                        string environment = _environment, Func<IZmqTransportConfiguration, ZmqTransport> transportFactory = null)
        {
            var configurationMock = new Mock<IZmqTransportConfiguration>();
            configurationMock.SetupGet(x => x.InboundEndPoint).Returns(endPoint);
            configurationMock.SetupGet(x => x.WaitForEndOfStreamAckTimeout).Returns(100.Milliseconds());

            var transport = transportFactory == null ? new ZmqTransport(configurationMock.Object, new ZmqSocketOptions()) : transportFactory(configurationMock.Object);

            transport.SocketOptions.SendTimeout = 10.Milliseconds();
            _transports.Add(transport);

            if (peerId != null)
                transport.Configure(new PeerId(peerId), environment);

            if (onMessageReceived != null)
                transport.MessageReceived += onMessageReceived;

            transport.Start();
            return transport;
        }

        private class CapturingIsListeningTimeZmqTransport : ZmqTransport
        {
            private readonly Stopwatch _stopwatch;
            private readonly object _lock = new object();
            public TimeSpan IsListeningSwitchTimestamp { get; private set; }

            public override bool IsListening
            {
                get
                {
                    lock (_lock)
                        return base.IsListening;
                }
                internal set
                {
                    lock (_lock)
                    {
                        base.IsListening = value;
                        if (!value)
                            IsListeningSwitchTimestamp = _stopwatch.Elapsed;
                    }
                }
            }


            public CapturingIsListeningTimeZmqTransport(IZmqTransportConfiguration configuration, Stopwatch stopwatch) : base(configuration, new ZmqSocketOptions())
            {
                _stopwatch = stopwatch;
                IsListeningSwitchTimestamp = TimeSpan.MaxValue;
            }
        }
    }
}