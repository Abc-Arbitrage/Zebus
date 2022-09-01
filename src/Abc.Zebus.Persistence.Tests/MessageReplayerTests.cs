using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Abc.Zebus.Persistence.Matching;
using Abc.Zebus.Persistence.Messages;
using Abc.Zebus.Persistence.Reporter;
using Abc.Zebus.Persistence.Storage;
using Abc.Zebus.Persistence.Tests.TestUtil;
using Abc.Zebus.Serialization;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Comparison;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Testing.Transport;
using Abc.Zebus.Transport;
using Abc.Zebus.Util;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Persistence.Tests
{
    public class MessageReplayerTests
    {
        private const int _replayBatchSize = 2;
        private const int _replaySwitchMessageCount = 2;

        private MessageReplayer _replayer;
        private Mock<IPersistenceConfiguration> _configurationMock;
        private TestBus _bus;
        private TestTransport _transport;
        private Mock<IInMemoryMessageMatcher> _messageMatcherMock;
        private Peer _selfPeer;
        private Peer _targetPeer;
        private Peer _anotherPeer;
        private Guid _replayId;
        private List<byte[]> _insertedMessages;
        private Mock<IStorage> _storageMock;
        private TransportMessageSerializer _transportMessageSerializer;

        [SetUp]
        public void Setup()
        {
            _transportMessageSerializer = new TransportMessageSerializer();
            _configurationMock = new Mock<IPersistenceConfiguration>();
            _configurationMock.Setup(conf => conf.SafetyPhaseDuration).Returns(500.Milliseconds());
            _configurationMock.SetupGet(x => x.ReplayBatchSize).Returns(_replayBatchSize);
            _configurationMock.SetupGet(x => x.ReplayUnackedMessageCountThatReleasesNextBatch).Returns(200);

            _selfPeer = new Peer(new PeerId("Abc.Testing.Self"), "tcp://abctest:888");
            _targetPeer = new Peer(new PeerId("Abc.Testing.Target"), "tcp://abcother:123");
            _anotherPeer = new Peer(new PeerId("Abc.Testing.Another"), "tcp://abcanother:123");

            _bus = new TestBus();
            _transport = new TestTransport(_selfPeer, "test");
            _messageMatcherMock = new Mock<IInMemoryMessageMatcher>();

            _replayId = Guid.NewGuid();

            _insertedMessages = new List<byte[]>();
            var readerMock = new Mock<IMessageReader>();
            _storageMock = new Mock<IStorage>();
            _storageMock.Setup(x => x.CreateMessageReader(It.IsAny<PeerId>())).Returns(readerMock.Object);

            readerMock.Setup(x => x.GetUnackedMessages()).Returns(_insertedMessages);

            var speedReporter = new Mock<IReporter>();

            _replayer = new MessageReplayer(_configurationMock.Object, _storageMock.Object, _bus, _transport, _messageMatcherMock.Object, _targetPeer, _replayId, speedReporter.Object, new MessageSerializer());

            _messageMatcherMock.Setup(x => x.EnqueueWaitHandle(It.IsAny<EventWaitHandle>())).Callback<EventWaitHandle>(x => x.Set());
        }

        [TearDown]
        public void Teardown()
        {
            MessageId.ResetLastTimestamp();
        }

        [Test]
        public void should_replay_messages()
        {
            var unackedTransportMessages = InsertMessagesInThePast(DateTime.UtcNow, messageCount: 11);
            Thread.Sleep(2);

            // make sure we have more than BatchSize messages in the same buckets
            for (int i = 0; i < _replayBatchSize; i++)
            {
                unackedTransportMessages.AddRange(InsertMessagesInThePast(DateTime.UtcNow, messageCount: 2));
                Thread.Sleep(2);
            }

            using (MessageId.PauseIdGeneration())
            {
                _replayer.Run(new CancellationToken());

                var replayMessages = _transport.Messages.Where(x => x.TransportMessage.MessageTypeId == MessageUtil.TypeId<MessageReplayed>()).ToList();
                replayMessages.Count.ShouldEqual(unackedTransportMessages.Count);

                unackedTransportMessages = unackedTransportMessages.OrderBy(msg => msg.Id.GetDateTime()).ToList();
                int messageIndex = 0;
                foreach (var unackedTransportMessage in unackedTransportMessages)
                {
                    AssertMessageWasReceived(unackedTransportMessage, messageIndex);
                    messageIndex++;
                }

                _bus.Expect(new ReplaySessionStarted(_targetPeer.Id, _replayId));
            }
        }

        [Test]
        public void should_not_replay_messages_if_peer_does_not_exist()
        {
            _storageMock.Setup(x => x.CreateMessageReader(It.IsAny<PeerId>())).Returns((IMessageReader)null);

            using (MessageId.PauseIdGeneration())
            {
                _replayer.Run(new CancellationToken());

                _bus.Expect(new ReplaySessionStarted(_targetPeer.Id, _replayId));
            }
        }

        private void AssertMessageWasReceived(TransportMessage unackedTransportMessage, int messageIndex)
        {
            var expected = new TransportMessageSent(new MessageReplayed(_replayId, unackedTransportMessage).ToTransportMessage(_selfPeer, wasPersisted: false), _targetPeer);
            var comparer = new MessageComparer();
            comparer.CheckExpectations(new[] { _transport.Messages[messageIndex] }, new[] { expected }, false);
        }

        [Test]
        public void should_send_replay_phase_ended()
        {
            using (MessageId.PauseIdGeneration())
            {
                _replayer.Run(new CancellationToken());

                _transport.Expect(new TransportMessageSent(new ReplayPhaseEnded(_replayId).ToTransportMessage(_selfPeer, wasPersisted: false), _targetPeer));
                _bus.Expect(new ReplaySessionEnded(_targetPeer.Id, _replayId));
            }
        }

        [Test]
        public void should_forward_live_messages_during_safety_phase()
        {
            using (MessageId.PauseIdGeneration())
            {
                var fakeMessage = new FakeCommand().ToTransportMessage(_anotherPeer, true);
                _replayer.AddLiveMessage(fakeMessage);

                _replayer.Run(new CancellationToken());

                _transport.Expect(
                    new TransportMessageSent(new ReplayPhaseEnded(_replayId).ToTransportMessage(_selfPeer, wasPersisted: false), _targetPeer),
                    new TransportMessageSent(new MessageReplayed(_replayId, fakeMessage).ToTransportMessage(_selfPeer, wasPersisted: false), _targetPeer)
                );
            }
        }

        [Test]
        public void should_send_safety_phase_ended()
        {
            using (MessageId.PauseIdGeneration())
            {
                _replayer.Run(new CancellationToken());

                _transport.Expect(new TransportMessageSent(new SafetyPhaseEnded(_replayId).ToTransportMessage(_selfPeer, wasPersisted: false), _targetPeer));
            }
        }

        [Test]
        public void should_start_and_wait_for_completion()
        {
            using (MessageId.PauseIdGeneration())
            {
                var stopped = false;

                _replayer.Stopped += () => stopped = true;
                _replayer.Start();
                _replayer.WaitForCompletion(10.Second());

                _transport.Expect(new TransportMessageSent(new SafetyPhaseEnded(_replayId).ToTransportMessage(_selfPeer, wasPersisted: false), _targetPeer));
                stopped.ShouldBeTrue();
            }
        }

        [Test]
        public void should_wait_for_batch_persistence_signal_before_start()
        {
            using (MessageId.PauseIdGeneration())
            {
                EventWaitHandle capturedSignal = null;
                _messageMatcherMock.Setup(x => x.EnqueueWaitHandle(It.IsAny<EventWaitHandle>())).Callback<EventWaitHandle>(s => capturedSignal = s);

                _replayer.Start();

                capturedSignal.ShouldNotBeNull();

                Thread.Sleep(100);
                _bus.Events.OfType<ReplaySessionStarted>().ShouldBeEmpty();

                capturedSignal.Set();
                Wait.Until(() => _bus.Events.OfType<ReplaySessionStarted>().Any(), 500.Milliseconds());

                _replayer.WaitForCompletion(10.Second());
            }
        }

        [Test]
        public void should_cancel_replayer()
        {
            using (MessageId.PauseIdGeneration())
            {
                _replayer.Start();
                _replayer.Cancel();

                _transport.Messages.ShouldNotContain(x => x.TransportMessage.MessageTypeId == new MessageTypeId(typeof(SafetyPhaseEnded)));
            }
        }

        [Test]
        public void should_wait_for_ack_from_before_sending_next_batch()
        {
            _replayer.UnackedMessageCountThatReleasesNextBatch = 1;

            var unackedTransportMessages = InsertMessagesInThePast(DateTime.Now, messageCount: 10);

            using (MessageId.PauseIdGeneration())
            {
                _replayer.Start();

                var messageIndex = 0;
                while (_transport.Messages.Count < unackedTransportMessages.Count + _replaySwitchMessageCount)
                {
                    while (_transport.Messages.Count > messageIndex && messageIndex < unackedTransportMessages.Count)
                    {
                        _replayer.OnMessageAcked(unackedTransportMessages[messageIndex].Id);
                        messageIndex++;
                    }

                    Thread.Sleep(10);
                }

                unackedTransportMessages = unackedTransportMessages.OrderBy(msg => msg.Id.GetDateTime()).ToList();
                messageIndex = 0;
                foreach (var unackedTransportMessage in unackedTransportMessages)
                {
                    AssertMessageWasReceived(unackedTransportMessage, messageIndex++);
                }
            }
        }

        [Test]
        public void should_cancel_replayer_waiting_for_acks()
        {
            InsertMessages(messageCount: 10);

            _replayer.UnackedMessageCountThatReleasesNextBatch = 4;
            _configurationMock.SetupGet(x => x.ReplayBatchSize).Returns(5);
            _replayer.Start();

            Wait.Until(() => _transport.Messages.Count > 1, 500.Milliseconds());

            _replayer.Cancel().ShouldBeTrue("Unable to cancel replayer");
        }

        private List<TransportMessage> InsertMessagesInThePast(DateTime refDateTime, int messageCount = 11)
        {
            MessageId.ResetLastTimestamp(); // because it goes back in the past!

            var refTime = refDateTime.AddHours(-messageCount);
            var transportMessages = new List<TransportMessage>();

            for (var i = 0; i < messageCount; ++i)
            {
                TransportMessage transportMessage;
                using (SystemDateTime.PauseTime(refTime))
                {
                    transportMessage = new FakeCommand(i).ToTransportMessage(_anotherPeer);
                }
                transportMessages.Add(transportMessage);

                _insertedMessages.AddRange(_transportMessageSerializer.Serialize(transportMessage));
                refTime = refTime.AddHours(1);
            }

            return transportMessages;
        }

        private void InsertMessages(int messageCount = 11)
        {
            MessageId.ResetLastTimestamp(); // because it goes back in the past!

            for (var i = 0; i < messageCount; ++i)
            {
                var transportMessage = new FakeCommand(i).ToTransportMessage(_anotherPeer);
                _insertedMessages.Add(_transportMessageSerializer.Serialize(transportMessage));
            }
        }
    }
}
