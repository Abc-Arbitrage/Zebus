using Abc.Zebus.Directory;
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
            foreach (var transport in _transports.Take(3))
            {
                StopZmqTransport(transport);
            }

            Parallel.ForEach(_transports.Skip(3), StopZmqTransport);

            static void StopZmqTransport(ZmqTransport transport)
            {
                try
                {
                    transport.Stop(true);
                }
                catch (Exception)
                {
                }
            }
        }

        [Test]
        public void should_not_crash_when_stopping_if_it_was_not_started()
        {
            var configuration = new ZmqTransportConfiguration { WaitForEndOfStreamAckTimeout = 100.Milliseconds() };
            var transport = new ZmqTransport(configuration, new ZmqSocketOptions(), new DefaultZmqOutboundSocketErrorHandler());

            Assert.That(transport.Stop, Throws.Nothing);
        }

        [Test]
        public void should_not_filter_received_messages_when_environment_is_not_specified()
        {
            var transport1 = CreateAndStartZmqTransport(environment: null);

            var transport2ReceivedMessages = new ConcurrentBag<TransportMessage>();
            var transport2 = CreateAndStartZmqTransport(onMessageReceived: transport2ReceivedMessages.Add, environment: "NotTest");
            var transport2Peer = transport2.GetPeer();

            var message = new FakeCommand(1).ToTransportMessage();
            transport1.Send(message, new[] { transport2Peer });

            Wait.Until(() => transport2ReceivedMessages.Count >= 1, 2.Seconds());
            transport2ReceivedMessages.Single().Id.ShouldEqual(message.Id);
        }

        [Test]
        public void should_not_let_the_outbound_thread_die_if_a_peer_cannot_be_resolved()
        {
            var senderTransport = CreateAndStartZmqTransport(environment: null);

            var receivedMessages = new ConcurrentBag<TransportMessage>();
            var destinationTransport = CreateAndStartZmqTransport(onMessageReceived: receivedMessages.Add, environment: "NotTest");
            var destinationPeer = destinationTransport.GetPeer();
            var nonExistingPeer = new Peer(new PeerId("Abc.NonExistingPeer.2"), "tcp://non-existing-peer:1234");

            var message = new FakeCommand(1).ToTransportMessage();
            senderTransport.Send(message, new[] { nonExistingPeer });
            senderTransport.Send(message, new[] { destinationPeer });

            Wait.Until(() => receivedMessages.Count >= 1, 2.Seconds(), "The outbound thread was killed and couldn't connect to the next peer");
        }

        [Test]
        public void should_not_dispatch_messages_received_from_wrong_environment()
        {
            var transport1ReceivedMessages = new ConcurrentBag<TransportMessage>();
            var transport1 = CreateAndStartZmqTransport(onMessageReceived: transport1ReceivedMessages.Add);

            var transport2ReceivedMessages = new ConcurrentBag<TransportMessage>();
            var transport2 = CreateAndStartZmqTransport(onMessageReceived: transport2ReceivedMessages.Add, environment: "NotTest");
            var transport2Peer = transport2.GetPeer();

            var message1 = new FakeCommand(1).ToTransportMessage();
            var message2 = new FakeCommand(2).ToTransportMessage();
            transport1.Send(message1, new[] { transport2Peer }); // should not arrive

            Thread.Sleep(500); //:(
            transport2.Configure(transport2Peer.Id, _environment);
            transport1.Send(message2, new[] { transport2Peer }); //should arrive

            Wait.Until(() => transport2ReceivedMessages.Count >= 1, 2.Seconds());
            transport2ReceivedMessages.Single().Id.ShouldEqual(message2.Id);
        }

        [Test]
        public void should_send_messages()
        {
            var transport1ReceivedMessages = new ConcurrentBag<TransportMessage>();
            var transport1 = CreateAndStartZmqTransport(onMessageReceived: transport1ReceivedMessages.Add);
            var transport1Peer = transport1.GetPeer();

            var transport2ReceivedMessages = new ConcurrentBag<TransportMessage>();
            var transport2 = CreateAndStartZmqTransport(onMessageReceived: transport2ReceivedMessages.Add);
            var transport2Peer = transport2.GetPeer();

            var message1 = new FakeCommand(1).ToTransportMessage();
            transport1.Send(message1, new[] { transport2Peer });

            Wait.Until(() => transport2ReceivedMessages.Count == 1, 2.Seconds());
            var transport2ReceivedMessage = transport2ReceivedMessages.ExpectedSingle();
            transport2ReceivedMessage.ShouldHaveSamePropertiesAs(message1, "Environment", "WasPersisted");
            transport2ReceivedMessage.Environment.ShouldEqual("Test");
            transport2ReceivedMessage.WasPersisted.ShouldEqual(false);

            var message2 = new FakeCommand(2).ToTransportMessage();
            transport2.Send(message2, new[] { transport1Peer });

            Wait.Until(() => transport1ReceivedMessages.Count == 1, 2.Seconds());
            var transport1ReceivedMessage = transport1ReceivedMessages.ExpectedSingle();
            transport1ReceivedMessage.ShouldHaveSamePropertiesAs(message2, "Environment", "WasPersisted");
            transport1ReceivedMessage.Environment.ShouldEqual("Test");
            transport1ReceivedMessage.WasPersisted.ShouldEqual(false);
        }

        [Test]
        public void should_send_message_to_peer_and_persistence()
        {
            // standard case: the message is forwarded to the persistence through SendContext.PersistencePeer
            // the target peer is up

            var senderTransport = CreateAndStartZmqTransport();

            var receiverMessages = new ConcurrentBag<TransportMessage>();
            var receiverTransport = CreateAndStartZmqTransport(onMessageReceived: receiverMessages.Add);
            var receiverPeer = receiverTransport.GetPeer();

            var persistenceMessages = new ConcurrentBag<TransportMessage>();
            var persistenceTransport = CreateAndStartZmqTransport(onMessageReceived: persistenceMessages.Add);
            var persistencePeer = persistenceTransport.GetPeer();

            var message = new FakeCommand(999).ToTransportMessage();
            senderTransport.Send(message, new[] { receiverPeer }, new SendContext { PersistentPeerIds = { receiverPeer.Id }, PersistencePeer = persistencePeer });

            Wait.Until(() => receiverMessages.Count == 1, 2.Seconds());
            var messageFromReceiver = receiverMessages.ExpectedSingle();
            messageFromReceiver.ShouldHaveSamePropertiesAs(message, "Environment", "WasPersisted");
            messageFromReceiver.Environment.ShouldEqual("Test");
            messageFromReceiver.WasPersisted.ShouldEqual(true);

            Wait.Until(() => persistenceMessages.Count == 1, 2.Seconds());
            var messageFromPersistence = persistenceMessages.ExpectedSingle();
            messageFromPersistence.ShouldHaveSamePropertiesAs(message, "Environment", "WasPersisted", "PersistentPeerIds", "IsPersistTransportMessage");
            messageFromPersistence.Environment.ShouldEqual("Test");
            messageFromPersistence.PersistentPeerIds.ShouldBeEquivalentTo(new[] { receiverPeer.Id });
        }

        [Test]
        public void should_send_message_to_persistence()
        {
            // standard case: the message is forwarded to the persistence through SendContext.PersistencePeer
            // the target peer is down

            var senderTransport = CreateAndStartZmqTransport();

            var receiverPeerId = new PeerId("Abc.R.0");

            var persistenceMessages = new ConcurrentBag<TransportMessage>();
            var persistenceTransport = CreateAndStartZmqTransport(onMessageReceived: persistenceMessages.Add);
            var persistencePeer = persistenceTransport.GetPeer();

            var message = new FakeCommand(999).ToTransportMessage();
            senderTransport.Send(message, Enumerable.Empty<Peer>(), new SendContext { PersistentPeerIds = { receiverPeerId }, PersistencePeer = persistencePeer });

            Wait.Until(() => persistenceMessages.Count == 1, 2.Seconds());
            var messageFromPersistence = persistenceMessages.ExpectedSingle();
            messageFromPersistence.ShouldHaveSamePropertiesAs(message, "Environment", "WasPersisted", "PersistentPeerIds", "IsPersistTransportMessage");
            messageFromPersistence.Environment.ShouldEqual("Test");
            messageFromPersistence.PersistentPeerIds.ShouldBeEquivalentTo(new[] { receiverPeerId });
        }

        [Test]
        public void should_send_persist_transport_message_to_persistence()
        {
            // edge case: the message is directly forwarded to the persistence

            var senderTransport = CreateAndStartZmqTransport();

            var receiverPeerId = new PeerId("Abc.Receiver.123");

            var persistenceMessages = new ConcurrentBag<TransportMessage>();
            var persistenceTransport = CreateAndStartZmqTransport(onMessageReceived: persistenceMessages.Add);
            var persistencePeer = persistenceTransport.GetPeer();

            var message = new FakeCommand(999).ToTransportMessage().ToPersistTransportMessage(receiverPeerId);
            senderTransport.Send(message, new[] { persistencePeer });

            Wait.Until(() => persistenceMessages.Count == 1, 2.Seconds());
            var messageFromPersistence = persistenceMessages.ExpectedSingle();
            messageFromPersistence.ShouldHaveSamePropertiesAs(message, "Environment", "WasPersisted");
            messageFromPersistence.Environment.ShouldEqual("Test");
            messageFromPersistence.PersistentPeerIds.ShouldBeEquivalentTo(new[] { receiverPeerId });
        }

        [Test]
        public void should_write_WasPersisted_when_requested()
        {
            var sender = CreateAndStartZmqTransport();

            var receivedMessages = new ConcurrentBag<TransportMessage>();
            var receiver = CreateAndStartZmqTransport(onMessageReceived: receivedMessages.Add);
            var receivingPeer = receiver.GetPeer();
            var message = new FakeCommand(1).ToTransportMessage();
            var otherMessage = new FakeCommand(2).ToTransportMessage();

            sender.Send(message, new[] { receivingPeer }, new SendContext { PersistentPeerIds = { receivingPeer.Id } });
            sender.Send(otherMessage, new[] { receivingPeer }, new SendContext());

            Wait.Until(() => receivedMessages.Count >= 2, 2.Seconds());
            receivedMessages.Single(x => x.Id == message.Id).WasPersisted.ShouldEqual(true);
            receivedMessages.Single(x => x.Id == otherMessage.Id).WasPersisted.ShouldEqual(false);
        }

        [Test]
        public void should_send_message_to_both_persisted_and_non_persisted_peers()
        {
            var sender = CreateAndStartZmqTransport();
            var receivedMessages = new ConcurrentBag<TransportMessage>();

            var receiver1 = CreateAndStartZmqTransport(onMessageReceived: receivedMessages.Add);
            var receivingPeer1 = receiver1.GetPeer();

            var receiver2 = CreateAndStartZmqTransport(onMessageReceived: receivedMessages.Add);
            var receivingPeer2 = receiver2.GetPeer();

            var message = new FakeCommand(1).ToTransportMessage();

            sender.Send(message, new[] { receivingPeer1, receivingPeer2 }, new SendContext { PersistentPeerIds = { receivingPeer1.Id } });

            Wait.Until(() => receivedMessages.Count >= 2, 2.Seconds());
            receivedMessages.ShouldContain(x => x.Id == message.Id && x.WasPersisted == true);
            receivedMessages.ShouldContain(x => x.Id == message.Id && x.WasPersisted == false);
        }

        [Test]
        public void should_support_peer_endpoint_modifications()
        {
            var senderTransport = CreateAndStartZmqTransport();

            var receivedMessages = new ConcurrentBag<TransportMessage>();
            var receiverTransport = CreateAndStartZmqTransport(onMessageReceived: receivedMessages.Add);
            var receiver = receiverTransport.GetPeer();

            senderTransport.Send(new FakeCommand(0).ToTransportMessage(), new[] { receiver });
            Wait.Until(() => receivedMessages.Count == 1, 2.Seconds());

            var newEndPoint = "tcp://127.0.0.1:" + TcpUtil.GetRandomUnusedPort();
            receiverTransport.Stop();
            receiverTransport = CreateAndStartZmqTransport(newEndPoint, receivedMessages.Add);
            receiver.EndPoint = receiverTransport.InboundEndPoint;

            senderTransport.Send(new FakeCommand(0).ToTransportMessage(), new[] { receiver });
            Wait.Until(() => receivedMessages.Count == 2, 2.Seconds(), "unable to receive message");
        }

        [Test, Repeat(5)]
        public void should_terminate_zmq_connection_of_a_forgotten_peer_after_some_time()
        {
            var senderTransport = CreateAndStartZmqTransport();
            var receiverTransport = CreateAndStartZmqTransport();
            var receiverPeer = receiverTransport.GetPeer();

            var message = new FakeCommand(1).ToTransportMessage();
            senderTransport.Send(message, new[] { receiverPeer });
            Wait.Until(() => senderTransport.OutboundSocketCount == 1, 2.Seconds());

            senderTransport.OnPeerUpdated(receiverPeer.Id, PeerUpdateAction.Decommissioned);

            Thread.Sleep(100);

            senderTransport.OutboundSocketCount.ShouldEqual(1);

            using (SystemDateTime.PauseTime(SystemDateTime.UtcNow.Add(30.Seconds())))
            {
                Wait.Until(() => senderTransport.OutboundSocketCount == 0, 1.Seconds(), "Socket should be disconnected");
            }
        }

        [Test, Repeat(5)]
        public void should_terminate_zmq_connection_of_a_started_peer_with_no_delay()
        {
            var senderTransport = CreateAndStartZmqTransport();
            var receiverTransport = CreateAndStartZmqTransport();
            var receiverPeer = receiverTransport.GetPeer();

            var message = new FakeCommand(1).ToTransportMessage();
            senderTransport.Send(message, new[] { receiverPeer });
            Wait.Until(() => senderTransport.OutboundSocketCount == 1, 2.Seconds());

            senderTransport.OnPeerUpdated(receiverPeer.Id, PeerUpdateAction.Started);

            Wait.Until(() => senderTransport.OutboundSocketCount == 0, 2.Seconds(), "Socket should be disconnected");
        }

        [Test]
        public void should_receive_many_messages()
        {
            var senderTransport = CreateAndStartZmqTransport();

            var receivedMessages = new List<TransportMessage>();
            var receiverTransport = CreateAndStartZmqTransport(onMessageReceived: receivedMessages.Add);
            var receiver = receiverTransport.GetPeer();

            for (var i = 0; i < 10; ++i)
            {
                var message = new FakeCommand(i).ToTransportMessage();
                senderTransport.Send(message, new[] { receiver });
            }

            Wait.Until(() => receivedMessages.Count == 10, 1.Second());

            for (var i = 0; i < 10; ++i)
            {
                var message = (FakeCommand)receivedMessages[i].ToMessage();
                message.FakeId.ShouldEqual(i);
            }
        }

        [Timeout(10 * 60 * 1000)]
        [TestCase(10)]
        [TestCase(25)]
        // Cases with high peer counts are too slow to run automatically, but they are required to validate edge cases.
        [TestCase(1000, Explicit = true)]
        [TestCase(1100, Explicit = true)]
        public void should_send_message_to_multiple_peers(int peerCount)
        {
            var senderTransport = CreateAndStartZmqTransport();

            var receivedMessagesCount = 0;
            var receiverTransports = Enumerable.Range(0, peerCount)
                                               .Select(_ => CreateZmqTransport(onMessageReceived: _ => Interlocked.Increment(ref receivedMessagesCount)))
                                               .ToList()
                                               .AsParallel()
                                               .Select(StartZmqTransport)
                                               .ToList();

            var message = new FakeCommand(999).ToTransportMessage();
            senderTransport.Send(message, receiverTransports.Select(x => x.GetPeer()));

            Wait.Until(() => Volatile.Read(ref receivedMessagesCount) == peerCount, 30.Second());
        }

        [Test]
        public void should_not_support_more_than_maximum_sockets()
        {
            const int maximumSocketCount = 4;

            var senderTransport = CreateAndStartZmqTransport(socketOptions: new ZmqSocketOptions
            {
                MaximumSocketCount = maximumSocketCount,
            });

            var receivedMessages = new List<TransportMessage>();
            var receiverTransports = Enumerable.Range(0, maximumSocketCount + 2)
                                               .Select(_ => CreateZmqTransport(onMessageReceived: receivedMessages.Add))
                                               .ToList()
                                               .AsParallel()
                                               .Select(StartZmqTransport)
                                               .ToList();

            var message = new FakeCommand(999).ToTransportMessage();
            senderTransport.Send(message, receiverTransports.Select(x => x.GetPeer()));

            Wait.Until(() => receivedMessages.Count == maximumSocketCount - 1, 10.Seconds());

            Thread.Sleep(1.Second());

            receivedMessages.Count.ShouldEqual(maximumSocketCount - 1);
        }

        [Test]
        public void should_not_block_when_hitting_high_water_mark()
        {
            var senderTransport = CreateAndStartZmqTransport(socketOptions: new ZmqSocketOptions
            {
                SendHighWaterMark = 3,
                SendTimeout = 50.Milliseconds(),
                SendRetriesBeforeSwitchingToClosedState = 2,
            });

            var receivedMessages = new List<TransportMessage>();
            var upReceiverTransport = CreateAndStartZmqTransport(onMessageReceived: receivedMessages.Add);
            var upReceiver = upReceiverTransport.GetPeer();

            var downReceiverTransport = CreateAndStartZmqTransport();
            var downReceiver = downReceiverTransport.GetPeer();

            downReceiverTransport.Stop();

            for (var i = 1; i <= 10; ++i)
            {
                var message = new FakeCommand(i).ToTransportMessage();
                senderTransport.Send(message, new[] { upReceiver, downReceiver });

                var expectedMessageCount = i;
                Wait.Until(() => receivedMessages.Count == expectedMessageCount, 2.Seconds(), "Failed to send message after " + i + " successful sent");
            }
        }

        [Test]
        public void should_not_wait_blocked_peers_on_every_send()
        {
            var senderTransport = CreateAndStartZmqTransport(socketOptions: new ZmqSocketOptions
            {
                SendHighWaterMark = 3,
                SendTimeout = 100.Milliseconds(),
                SendRetriesBeforeSwitchingToClosedState = 0,
            });

            var receivedMessages = new List<TransportMessage>();
            var upReceiverTransport = CreateAndStartZmqTransport( onMessageReceived: receivedMessages.Add);
            var upReceiver = upReceiverTransport.GetPeer();

            var downReceiverTransport = CreateAndStartZmqTransport();
            var downReceiver = downReceiverTransport.GetPeer();

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
            Wait.Until(() => receivedMessages.Count == 10, 10.Seconds(), "Timed out while waiting for messages");
            receiverStopwatch.Stop();
            Console.WriteLine("Elapsed time to get messages: " + receiverStopwatch.Elapsed);
            receiverStopwatch.ElapsedMilliseconds.ShouldBeLessOrEqualThan(1000, "Throughput is too low");
        }

        [Test]
        public void should_not_wait_for_unknown_peer_on_every_send()
        {
            var receivedMessageCount = 0;
            var senderTransport = CreateAndStartZmqTransport();
            var receiverTransport = CreateAndStartZmqTransport(onMessageReceived: _ => receivedMessageCount++);
            var receiver = receiverTransport.GetPeer();
            var invalidPeer = new Peer(new PeerId("Abc.Testing.Invalid"), "tcp://unknown-bastard:123456");

            for (var i = 0; i < 1000; i++)
            {
                var message = new FakeCommand(i).ToTransportMessage();
                senderTransport.Send(message, new[] { invalidPeer, receiver });
            }

            Wait.Until(() => receivedMessageCount == 1000, 5.Seconds());
        }

        [Test]
        public void should_send_various_sized_messages()
        {
            var senderTransport = CreateAndStartZmqTransport(socketOptions: new ZmqSocketOptions
            {
                SendHighWaterMark = 3,
            });

            var receivedMessages = new List<TransportMessage>();
            var receiverTransport = CreateAndStartZmqTransport(onMessageReceived: receivedMessages.Add);
            var receiver = receiverTransport.GetPeer();

            var messageBytes = new byte[5000];
            new Random().NextBytes(messageBytes);

            var bigMessage = new TransportMessage(new MessageTypeId(typeof(FakeCommand)), messageBytes, new PeerId("X"), senderTransport.InboundEndPoint);
            senderTransport.Send(bigMessage, new[] { receiver });

            Wait.Until(() => receivedMessages.Count == 1, 2.Seconds());

            receivedMessages[0].ShouldHaveSamePropertiesAs(bigMessage, "Environment", "WasPersisted");

            var smallMessage = new TransportMessage(new MessageTypeId(typeof(FakeCommand)), new byte[1], new PeerId("X"), senderTransport.InboundEndPoint);
            senderTransport.Send(smallMessage, new[] { receiver });

            Wait.Until(() => receivedMessages.Count == 2, 2.Seconds());

            receivedMessages[1].ShouldHaveSamePropertiesAs(smallMessage, "Environment", "WasPersisted");
        }

        [Test]
        public void should_send_message_to_self()
        {
            var receivedMessages = new List<TransportMessage>();
            var transport = CreateAndStartZmqTransport(onMessageReceived: receivedMessages.Add);
            var self = transport.GetPeer();

            transport.Send(new FakeCommand(1).ToTransportMessage(), new[] { self });

            Wait.Until(() => receivedMessages.Count == 1, 2.Seconds());
        }

        [Test]
        public void should_not_forward_messages_to_upper_layer_when_stopping()
        {
            var receivedMessages = new List<TransportMessage>();
            var receivingTransport = CreateAndStartZmqTransport(onMessageReceived: receivedMessages.Add);
            var receivingPeer = receivingTransport.GetPeer();

            var receivedWhileNotListening = false;
            receivingTransport.MessageReceived += _ => receivedWhileNotListening |= !receivingTransport.IsListening;

            var sendingTransport = CreateAndStartZmqTransport();
            var shouldSendMessages = true;

            Task.Run(() =>
            {
                var sendCount = 0;
                var spinWait = new SpinWait();
                // ReSharper disable once AccessToModifiedClosure
                while (shouldSendMessages)
                {
                    sendingTransport.Send(new FakeCommand(0).ToTransportMessage(), new[] { receivingPeer });
                    sendCount++;
                    spinWait.SpinOnce();
                }
                Console.WriteLine($"{sendCount} messages sent");
            });

            Wait.Until(() => receivedMessages.Count > 1, 10.Seconds());
            Console.WriteLine("Message received");

            receivingTransport.Stop();
            Console.WriteLine("Receiving transport stopped");

            receivedWhileNotListening.ShouldBeFalse();
            shouldSendMessages = false;

            sendingTransport.Stop();
            Console.WriteLine("Sending transport stopped");
        }

        [Test]
        public void should_process_all_messages_in_buffer_on_stop()
        {
            var receivedMessageCount = 0;
            var sentMessageCount = 0;
            var shouldSend = new[] { true, };

            var receivingTransport = CreateAndStartZmqTransport(onMessageReceived: _ => receivedMessageCount++);
            var sendingTransport = CreateAndStartZmqTransport();
            var receivingPeer = receivingTransport.GetPeer();

            var senderTask = new Thread(() =>
            {
                Log($"Send loop started");

                while (shouldSend[0])
                {
                    sendingTransport.Send(new FakeCommand(sentMessageCount++).ToTransportMessage(), new[] { receivingPeer });
                }

                Log($"Send loop terminated, Count: {sentMessageCount}");

                sendingTransport.Stop();

                Log($"Sender stopped");
            });

            senderTask.Start();
            Wait.Until(() => receivedMessageCount != 0, 2.Seconds());

            Log($"Stopping the sender");
            shouldSend[0] = false;
            senderTask.Join();

            Log($"Stopping the receiver");
            receivingTransport.Stop();
            Log($"Receiver stopped");

            Thread.MemoryBarrier();
            if (receivedMessageCount != sentMessageCount)
                Thread.Sleep(1.Second());

            receivedMessageCount.ShouldEqual(sentMessageCount);

            static void Log(string text) => Console.WriteLine(DateTime.Now.TimeOfDay + " " + text + Environment.NewLine + Environment.NewLine);
        }

        [Test]
        public void should_disconnect_peer_socket_of_a_stopped_peer_after_some_time()
        {
            var transport1 = CreateAndStartZmqTransport();
            var peer1 = transport1.GetPeer();

            var transport2 = CreateAndStartZmqTransport();
            var peer2 = transport2.GetPeer();

            transport1.Send(new FakeCommand(0).ToTransportMessage(), new[] { peer2 });
            transport2.Send(new FakeCommand(0).ToTransportMessage(), new[] { peer1 });
            Wait.Until(() => transport1.OutboundSocketCount == 1, 10.Seconds());
            Wait.Until(() => transport2.OutboundSocketCount == 1, 10.Seconds());

            transport2.Stop();

            Wait.Until(() => transport1.OutboundSocketCount == 0, 10.Seconds());
        }

        private ZmqTransport CreateZmqTransport(string endPoint = "tcp://*:*", Action<TransportMessage> onMessageReceived = null, string peerId = null, string environment = _environment, ZmqSocketOptions socketOptions = null)
        {
            var configuration = new ZmqTransportConfiguration(endPoint)
            {
                WaitForEndOfStreamAckTimeout = 1.Second(),
            };

            // Previous code used a specific SendTimeout of 500 ms for unknown reasons.
            var effectiveSocketOptions = socketOptions ?? new ZmqSocketOptions();

            var transport = new ZmqTransport(configuration, effectiveSocketOptions, new DefaultZmqOutboundSocketErrorHandler());
            transport.SetLogId(_transports.Count);

            _transports.Add(transport);

            var effectivePeerId = new PeerId(peerId ?? $"Abc.Testing.{Guid.NewGuid():N}");
            transport.Configure(effectivePeerId, environment);

            if (onMessageReceived != null)
                transport.MessageReceived += onMessageReceived;

            return transport;
        }

        private ZmqTransport CreateAndStartZmqTransport(string endPoint = "tcp://*:*", Action<TransportMessage> onMessageReceived = null, string peerId = null, string environment = _environment, ZmqSocketOptions socketOptions = null)
        {
            return StartZmqTransport(CreateZmqTransport(endPoint, onMessageReceived, peerId, environment, socketOptions));
        }

        private static ZmqTransport StartZmqTransport(ZmqTransport transport)
        {
            transport.Start();

            return transport;
        }
    }
}
