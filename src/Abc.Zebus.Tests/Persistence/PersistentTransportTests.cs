using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Abc.Zebus.Core;
using Abc.Zebus.Directory;
using Abc.Zebus.Persistence;
using Abc.Zebus.Routing;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Testing.Transport;
using Abc.Zebus.Tests.Messages;
using Abc.Zebus.Transport;
using Abc.Zebus.Util;
using Abc.Zebus.Util.Extensions;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Persistence
{
    public class PersistentTransportTests : PersistentTransportFixture
    {
        protected override bool IsPersistent => true;

        private Guid ReplayId => StartMessageReplayCommand.ReplayId;

        [Test]
        public void starting_should_start_inner_bus_and_send_replay_command()
        {
            StartMessageReplayCommand.ShouldNotBeNull();
            StartMessageReplayCommandTargets.ShouldBeEquivalentTo(new[] { PersistencePeer });
        }

        [Test]
        public void should_not_crash_when_stopping_if_it_was_not_started()
        {
            Assert.That(() => Transport.Stop(), Throws.Nothing);
        }

        [Test]
        public void should_only_forward_replayed_messages_during_replay_phase()
        {
            Transport.Start();
            var transportMessageToForward = new FakeCommand(123).ToTransportMessage();
            var normalTransportMessage = new FakeEvent(123).ToTransportMessage();

            var replayedTransportMessage = transportMessageToForward.ToReplayedTransportMessage(ReplayId);
            InnerTransport.RaiseMessageReceived(replayedTransportMessage);
            InnerTransport.RaiseMessageReceived(normalTransportMessage);

            MessagesForwardedToBus.Count.ShouldEqual(1);
            MessagesForwardedToBus.Single().Id.ShouldEqual(transportMessageToForward.Id);
        }

        [Test]
        public void should_force_WasPersisted_for_replayed_messages()
        {
            Transport.Start();

            var sourceTransportMessage = new FakeCommand(123).ToTransportMessage();
            sourceTransportMessage.WasPersisted = null;

            var replayTransportMessage = sourceTransportMessage.ToReplayedTransportMessage(ReplayId);
            InnerTransport.RaiseMessageReceived(replayTransportMessage);

            var forwardedTransportMessage = MessagesForwardedToBus.ExpectedSingle();
            forwardedTransportMessage.WasPersisted.ShouldEqual(true);
        }

        [Test]
        public void should_force_WasPersisted_for_replayed_messages_during_safety_phase()
        {
            Transport.Start();

            InnerTransport.RaiseMessageReceived(new ReplayPhaseEnded(ReplayId).ToTransportMessage());

            var sourceTransportMessage = new FakeCommand(123).ToTransportMessage();
            sourceTransportMessage.WasPersisted = null;

            var replayTransportMessage = sourceTransportMessage.ToReplayedTransportMessage(ReplayId);
            InnerTransport.RaiseMessageReceived(replayTransportMessage);

            Wait.Until(() => MessagesForwardedToBus.Count == 1, 2.Seconds());

            var forwardedTransportMessage = MessagesForwardedToBus.ExpectedSingle();
            forwardedTransportMessage.WasPersisted.ShouldEqual(true);
        }

        [Test]
        public void should_forward_a_normal_message_after_a_back_to_live_event()
        {
            Transport.Start();

            var transportMessageToForward = new FakeCommand(123).ToTransportMessage();
            InnerTransport.RaiseMessageReceived(transportMessageToForward);
            MessagesForwardedToBus.ShouldBeEmpty();

            InnerTransport.RaiseMessageReceived(new ReplayPhaseEnded(StartMessageReplayCommand.ReplayId).ToTransportMessage());

            Wait.Until(() => MessagesForwardedToBus.Count == 1, 2.Seconds());

            var transportMessage = MessagesForwardedToBus.Single();
            transportMessage.ShouldEqualDeeply(transportMessageToForward);
        }

        [Test, Repeat(20)]
        public void should_not_lose_messages_when_switching_to_safety_phase()
        {
            Transport.Start();

            var liveMessageToStack = new FakeCommand(123).ToTransportMessage();
            var replayedMessageToPlayAfterStack = new FakeCommand(456).ToTransportMessage();

            InnerTransport.RaiseMessageReceived(liveMessageToStack);
            InnerTransport.RaiseMessageReceived(new ReplayPhaseEnded(StartMessageReplayCommand.ReplayId).ToTransportMessage());
            InnerTransport.RaiseMessageReceived(replayedMessageToPlayAfterStack.ToReplayedTransportMessage(StartMessageReplayCommand.ReplayId));

            Wait.Until(() => MessagesForwardedToBus.Count >= 2, 2.Seconds());

            MessagesForwardedToBus.Count.ShouldEqual(2);
            MessagesForwardedToBus.First().Id.ShouldEqual(liveMessageToStack.Id);
            MessagesForwardedToBus.Last().Id.ShouldEqual(replayedMessageToPlayAfterStack.Id);
        }

        [Test]
        public void should_not_crash_during_safety_phase()
        {
            Transport.Start();

            var failingMessage = new FakeCommand(666).ToTransportMessage();
            var otherMessage = new FakeCommand(123).ToTransportMessage();
            var successfullyReceivedMessages = new List<TransportMessage>();
            Transport.MessageReceived += msg =>
            {
                if (msg == failingMessage)
                    throw new Exception("Failure");
                successfullyReceivedMessages.Add(msg);

            };

            InnerTransport.RaiseMessageReceived(new ReplayPhaseEnded(StartMessageReplayCommand.ReplayId).ToTransportMessage());
            InnerTransport.RaiseMessageReceived(failingMessage);
            InnerTransport.RaiseMessageReceived(otherMessage);

            Wait.Until(() => successfullyReceivedMessages.Count >= 1, 2.Seconds());

            successfullyReceivedMessages.Single().ShouldEqual(otherMessage);
        }

        [Test]
        public void should_not_handle_twice_duplicate_messages()
        {
            Transport.Start();

            var duplicatedMessage = new FakeCommand(123).ToTransportMessage();

            InnerTransport.RaiseMessageReceived(new ReplayPhaseEnded(StartMessageReplayCommand.ReplayId).ToTransportMessage());
            InnerTransport.RaiseMessageReceived(duplicatedMessage);
            InnerTransport.RaiseMessageReceived(duplicatedMessage.ToReplayedTransportMessage(StartMessageReplayCommand.ReplayId));

            Wait.Until(() => MessagesForwardedToBus.Count == 1, 2.Seconds());

            MessagesForwardedToBus.Single().Id.ShouldEqual(duplicatedMessage.Id);
        }

        [Test]
        public void should_not_handle_replayed_message_with_unknown_replay_id()
        {
            var otherReplayId = Guid.NewGuid();

            var message = new FakeCommand(123).ToTransportMessage();
            InnerTransport.RaiseMessageReceived(message.ToReplayedTransportMessage(otherReplayId));

            Thread.Sleep(10);
            MessagesForwardedToBus.Count.ShouldEqual(0);
        }

        [Test]
        public void should_forward_normal_message_after_replay_phase()
        {
            Transport.Start();

            var message = new FakeCommand(123).ToTransportMessage();

            InnerTransport.RaiseMessageReceived(new ReplayPhaseEnded(StartMessageReplayCommand.ReplayId).ToTransportMessage());
            Thread.Sleep(10);
            InnerTransport.RaiseMessageReceived(message);
            Thread.Sleep(10);

            MessagesForwardedToBus.Count.ShouldEqual(1);
            MessagesForwardedToBus.Single().Id.ShouldEqual(message.Id);
        }

        [Test]
        public void should_stop_to_deduplicate_after_safety_phase()
        {
            Transport.Start();
            var duplicatedMessage = new FakeCommand(123).ToTransportMessage();

            InnerTransport.RaiseMessageReceived(new ReplayPhaseEnded(StartMessageReplayCommand.ReplayId).ToTransportMessage());
            InnerTransport.RaiseMessageReceived(new SafetyPhaseEnded(StartMessageReplayCommand.ReplayId).ToTransportMessage());
            InnerTransport.RaiseMessageReceived(duplicatedMessage);
            InnerTransport.RaiseMessageReceived(duplicatedMessage);

            Wait.Until(() => MessagesForwardedToBus.Count == 2, 2.Seconds());

            MessagesForwardedToBus.ForEach(msg => msg.Id.ShouldEqual(duplicatedMessage.Id));
        }

        [Test]
        public void should_handle_execution_completion_received()
        {
            var messageExecutionCompletedTransportMessage = new MessageExecutionCompleted(MessageId.NextId(), 0, null).ToTransportMessage();

            InnerTransport.RaiseMessageReceived(messageExecutionCompletedTransportMessage);

            var forwardedMessage = MessagesForwardedToBus.Single(x => x.MessageTypeId == MessageExecutionCompleted.TypeId);
            forwardedMessage.ShouldEqualDeeply(messageExecutionCompletedTransportMessage);
        }

        [Test]
        public void should_send_persistent_message_to_the_persistence_and_to_the_peer()
        {
            using (MessageId.PauseIdGeneration())
            {
                var message = new FakeCommand(123).ToTransportMessage();

                Transport.Send(message, new[] { AnotherPersistentPeer });

                InnerTransport.ExpectExactly(new[]
                {
                    new TransportMessageSent(message).To(AnotherPersistentPeer, true).ToPersistence(PersistencePeer),
                });
            }
        }

        [Test]
        public void should_send_persistent_message_only_to_the_persistence_when_peer_is_down()
        {
            using (MessageId.PauseIdGeneration())
            {
                var message = new FakeCommand(123).ToTransportMessage();
                var downPeer = new Peer(new PeerId("Another.Peer"), "tcp://anotherpeer:123", false);

                Transport.Send(message, new[] { downPeer });

                InnerTransport.ExpectExactly(new TransportMessageSent(message).ToPersistence(PersistencePeer).AddPersistentPeer(downPeer));
            }
        }

        [Test]
        public void non_persistent_messages_should_not_be_sent_to_persistence_peer()
        {
            using (MessageId.PauseIdGeneration())
            {
                var message = new FakeNonPersistentCommand(123).ToTransportMessage();

                Transport.Send(message, new[] { AnotherPersistentPeer });

                InnerTransport.ExpectExactly(new TransportMessageSent(message, new[] { AnotherPersistentPeer }));
            }
        }

        [Test]
        public void persistent_messages_sent_to_multiple_peers_should_only_be_persisted_for_persistent_ones()
        {
            using (MessageId.PauseIdGeneration())
            {
                var message = new FakeEvent(123).ToTransportMessage(Self);

                Transport.Send(message, new[] { AnotherPersistentPeer, AnotherNonPersistentPeer });

                InnerTransport.ExpectExactly(new []
                {
                    new TransportMessageSent(message).To(AnotherPersistentPeer, true).To(AnotherNonPersistentPeer, false).ToPersistence(PersistencePeer),
                });
            }
        }

        [Test]
        public void persistent_messages_sent_to_non_persistent_peers_should_not_generate_a_persist_command()
        {
            using (MessageId.PauseIdGeneration())
            {
                var message = new FakeEvent(123).ToTransportMessage(Self);

                Transport.Send(message, new[] { AnotherNonPersistentPeer });

                InnerTransport.ExpectExactly(new TransportMessageSent(message, AnotherNonPersistentPeer));
            }
        }

        [Test]
        public void should_publish_a_MessageHandled_event_after_a_persistent_message_is_processed_by_the_bus()
        {
            Transport.Start();
            using (MessageId.PauseIdGeneration())
            {
                var command = new FakeCommand(123).ToTransportMessage();
                InnerTransport.RaiseMessageReceived(new ReplayPhaseEnded(StartMessageReplayCommand.ReplayId).ToTransportMessage());
                InnerTransport.RaiseMessageReceived(command);

                Transport.AckMessage(command);

                var messageHandledMessage = new MessageHandled(command.Id).ToTransportMessage(Self);
                InnerTransport.ExpectExactly(new TransportMessageSent(messageHandledMessage, PersistencePeer));
            }
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void should_consider_WasPersisted_before_publishing_a_MessageHandled_event(bool wasPersisted)
        {
            Transport.Start();
            using (MessageId.PauseIdGeneration())
            {
                var command = new FakeCommand(123).ToTransportMessage(wasPersisted: wasPersisted);
                InnerTransport.RaiseMessageReceived(new ReplayPhaseEnded(StartMessageReplayCommand.ReplayId).ToTransportMessage());

                InnerTransport.RaiseMessageReceived(command);
                Transport.AckMessage(command);

                if (wasPersisted)
                    InnerTransport.ExpectExactly(new TransportMessageSent(new MessageHandled(command.Id).ToTransportMessage(Self), PersistencePeer));
                else
                    InnerTransport.ExpectNothing();
            }
        }

        [Test]
        public void should_not_publish_a_MessageHandled_event_after_a_non_persistent_message_is_processed_by_the_bus()
        {
            Transport.Start();

            using (MessageId.PauseIdGeneration())
            {
                var command = new FakeNonPersistentCommand(123).ToTransportMessage();
                InnerTransport.RaiseMessageReceived(new ReplayPhaseEnded(StartMessageReplayCommand.ReplayId).ToTransportMessage());

                InnerTransport.RaiseMessageReceived(command);
                Transport.AckMessage(command);

                InnerTransport.ExpectNothing();
            }
        }

        [Test]
        public void should_not_lose_messages_when_persistent_transport_goes_down_and_comes_back_up()
        {
            using(MessageId.PauseIdGeneration())
            {
                // Stopping persistence
                InnerTransport.RaiseMessageReceived(new TransportMessage(MessageTypeId.PersistenceStopping, new MemoryStream(), PersistencePeer));
                InnerTransport.ExpectExactly(new TransportMessageSent(new TransportMessage(MessageTypeId.PersistenceStoppingAck, new MemoryStream(), Self), PersistencePeer));

                InnerTransport.Messages.Clear();

                // should enqueue messages to persistence
                var message = new FakeCommand(123).ToTransportMessage();
                var ackedMessage = new FakeCommand(456).ToTransportMessage();
                Transport.Send(message, new[] { AnotherPersistentPeer });
                Transport.AckMessage(ackedMessage);

                InnerTransport.AckedMessages.ShouldBeEmpty();
                InnerTransport.ExpectExactly(new TransportMessageSent(message, AnotherPersistentPeer, true));
                InnerTransport.Messages.Clear();

                // starting persistence - should send enqueued messages
                Transport.OnPeerUpdated(PersistencePeer.Id, PeerUpdateAction.Started);

                InnerTransport.ExpectExactly(new[]
                {
                    new TransportMessageSent(message.ToPersistTransportMessage(AnotherPersistentPeer.Id), PersistencePeer),
                    new TransportMessageSent(new MessageHandled(ackedMessage.Id).ToTransportMessage(Self), PersistencePeer)
                });

                InnerTransport.Messages.Clear();
                InnerTransport.AckedMessages.Clear();

                // should send messages without going through the queue
                Transport.Send(message, new[] { AnotherPersistentPeer });
                Transport.AckMessage(ackedMessage);

                InnerTransport.ExpectExactly(new[]
                {
                    new TransportMessageSent(message, AnotherPersistentPeer, true).ToPersistence(PersistencePeer),
                    new TransportMessageSent(new MessageHandled(ackedMessage.Id).ToTransportMessage(Self), PersistencePeer),
                });
            }
        }

        [Test]
        public void should_not_send_messages_to_persistence_twice_if_persistence_goes_up_and_down()
        {
            using (MessageId.PauseIdGeneration())
            {
                // Stopping persistence
                InnerTransport.RaiseMessageReceived(new TransportMessage(MessageTypeId.PersistenceStopping, new MemoryStream(), PersistencePeer));
                InnerTransport.Messages.Clear();

                var ackedMessage = new FakeCommand(456).ToTransportMessage();
                Transport.AckMessage(ackedMessage);
                InnerTransport.AckedMessages.ShouldBeEmpty();

                // starting persistence - should send enqueued messages
                Transport.OnPeerUpdated(PersistencePeer.Id, PeerUpdateAction.Started);

                InnerTransport.ExpectExactly(new[]
                {
                    new TransportMessageSent(new MessageHandled(ackedMessage.Id).ToTransportMessage(Self), PersistencePeer)
                });

                // Stopping persistence again
                InnerTransport.RaiseMessageReceived(new TransportMessage(MessageTypeId.PersistenceStopping, new MemoryStream(), PersistencePeer));
                InnerTransport.Messages.Clear();

                // starting persistence again - should not have anything to send
                Transport.OnPeerUpdated(PersistencePeer.Id, PeerUpdateAction.Started);

                InnerTransport.ExpectNothing();
            }
        }

        [Test]
        public void should_discard_messages_waiting_for_persistence_on_stop()
        {
            using (MessageId.PauseIdGeneration())
            {
                Transport.Start();

                PeerDirectory.Setup(directory => directory.GetPeersHandlingMessage(new MessageBinding(MessageTypeId.PersistenceStoppingAck, BindingKey.Empty)))
                             .Returns(new List<Peer> { PersistencePeer });

                // Stopping persistence
                InnerTransport.RaiseMessageReceived(new TransportMessage(MessageTypeId.PersistenceStopping, new MemoryStream(), PersistencePeer));
                InnerTransport.Messages.Clear();

                var ackedMessage = new FakeCommand(456).ToTransportMessage();
                Transport.AckMessage(ackedMessage);

                Transport.Stop();
                Transport.Start();
            }
        }
    }
}