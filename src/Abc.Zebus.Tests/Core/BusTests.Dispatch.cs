using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Abc.Zebus.Core;
using Abc.Zebus.Directory;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Persistence;
using Abc.Zebus.Routing;
using Abc.Zebus.Subscriptions;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Dispatch;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Testing.Transport;
using Abc.Zebus.Testing.UnitTesting;
using Abc.Zebus.Tests.Messages;
using Abc.Zebus.Tests.Scan;
using Abc.Zebus.Transport;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Core
{
    public partial class BusTests
    {
        public class Dispatch : BusTests
        {
            private MessageDispatch _dispatchedMessage;
            private readonly Peer _otherPeer = new Peer(new PeerId("Abc.Testing.1"), "tcp://abctest:789");

            [Test]
            public void should_dispatch_received_message()
            {
                var command = new FakeCommand(123);
                var invokerCalled = false;
                SetupDispatch(command, _ => invokerCalled = true);

                var transportMessageReceived = command.ToTransportMessage(_peerUp);
                _transport.RaiseMessageReceived(transportMessageReceived);

                invokerCalled.ShouldBeTrue();
            }

            [Test]
            public void should_dispatch_received_empty_message()
            {
                var command = new EmptyCommand();
                var invokerCalled = false;
                SetupDispatch(command, _ => invokerCalled = true);

                var transportMessageReceived = command.ToTransportMessage(_peerUp);
                transportMessageReceived.Content = default;
                _transport.RaiseMessageReceived(transportMessageReceived);

                invokerCalled.ShouldBeTrue();
            }

            [Test]
            public void should_handle_command_locally()
            {
                var command = new FakeCommand(1);
                var handled = false;
                SetupDispatch(command, _ => handled = true);
                SetupPeersHandlingMessage<FakeCommand>(_self);

                _bus.Start();

                var completed = _bus.Send(command).Wait(500);

                handled.ShouldBeTrue();
                completed.ShouldBeTrue();
                _transport.Messages.ShouldBeEmpty();
            }

            [Test]
            public void should_forward_error_when_handling_command_locally()
            {
                var command = new FakeCommand(1);
                SetupDispatch(command, error: new Exception("Test error"));
                SetupPeersHandlingMessage<FakeCommand>(_self);

                _bus.Start();

                var task = _bus.Send(command);
                var completed = task.Wait(500);

                completed.ShouldBeTrue();
                task.Result.IsSuccess.ShouldBeFalse();
            }

            [Test]
            public void should_not_handle_command_locally_when_local_dispatch_is_disabled()
            {
                var command = new FakeCommand(1);
                var handled = false;
                SetupDispatch(command, _ => handled = true);
                SetupPeersHandlingMessage<FakeCommand>(_self);

                _bus.Start();

                using (LocalDispatch.Disable())
                using (MessageId.PauseIdGeneration())
                {
                    var completed = _bus.Send(command).Wait(5);

                    handled.ShouldBeFalse();
                    completed.ShouldBeFalse();
                    _transport.ExpectExactly(new TransportMessageSent(command.ToTransportMessage(_self), _self));
                }
            }

            [Test]
            public void should_handle_event_locally()
            {
                var message = new FakeEvent(1);
                var handled = false;
                SetupDispatch(message, x => handled = true);
                SetupPeersHandlingMessage<FakeEvent>(_self, _peerUp);

                _bus.Start();
                _bus.Publish(message);

                handled.ShouldBeTrue();

                var sentMessage = _transport.Messages.Single();
                sentMessage.Targets.Single().ShouldEqual(_peerUp);
            }

            [Test]
            public void should_not_handle_event_locally_when_local_dispatch_is_disabled()
            {
                var message = new FakeEvent(1);
                var handled = false;
                SetupDispatch(message, x => handled = true);
                SetupPeersHandlingMessage<FakeEvent>(_self);

                _bus.Start();

                using (LocalDispatch.Disable())
                using (MessageId.PauseIdGeneration())
                {
                    _bus.Publish(message);

                    handled.ShouldBeFalse();

                    _transport.ExpectExactly(new TransportMessageSent(message.ToTransportMessage(_self), _self));
                }
            }

            [Test]
            public void should_ack_transport_when_dispatch_done()
            {
                var command = new FakeCommand(123);
                SetupDispatch(command);
                SetupPeersHandlingMessage<FakeCommand>(_peerUp);

                _bus.Start();

                var task = _bus.Send(command);
                var transportMessage = command.ToTransportMessage();
                _transport.RaiseMessageReceived(transportMessage);

                task.Wait(10);

                _transport.AckedMessages.Count.ShouldEqual(1);
                _transport.AckedMessages[0].Id.ShouldEqual(transportMessage.Id);
            }

            [Test]
            public void should_create_message_dispatch()
            {
                var command = new FakeCommand(123);
                var dispatch = _bus.CreateMessageDispatch(command.ToTransportMessage());

                dispatch.Message.ShouldEqualDeeply(command);
            }

            [Test]
            public void should_create_custom_message_dispatch_for_PersistMessageCommand()
            {
                var command = new FakeCommand(123);
                var transportMessage = command.ToTransportMessage();
                transportMessage.PersistentPeerIds = new List<PeerId> { new PeerId("Abc.SomePersistentPeer.0") };
                var dispatch = _bus.CreateMessageDispatch(transportMessage);

                var persistCommand = dispatch.Message as PersistMessageCommand;
                persistCommand.ShouldNotBeNull();
                persistCommand.Targets.ShouldBeEquivalentTo(transportMessage.PersistentPeerIds);
                persistCommand.TransportMessage.ShouldHaveSamePropertiesAs(transportMessage, "IsPersistTransportMessage", "PersistentPeerIds");
            }

            [Test]
            public void should_create_message_dispatch_for_empty_message()
            {
                var command = new EmptyCommand();
                var transportMessage = command.ToTransportMessage();
                transportMessage.Content = default;
                var dispatch = _bus.CreateMessageDispatch(transportMessage);

                dispatch.Message.ShouldEqualDeeply(command);
            }

            [Test]
            public void should_not_send_acknowledgement_when_message_handled()
            {
                var command = new FakeCommand(123);
                var dispatch = _bus.CreateMessageDispatch(command.ToTransportMessage());

                dispatch.SetHandlerCount(1);
                dispatch.SetHandled(null, null);

                _transport.ExpectNothing();
            }

            [Test]
            public void should_stop_dispatcher_before_transport()
            {
                var transportMock = new Mock<ITransport>();
                var bus = new Bus(transportMock.Object, _directoryMock.Object, _messageSerializer, _messageDispatcherMock.Object, new DefaultMessageSendingStrategy(), new DefaultStoppingStrategy(), _configuration);
                var sequence = new SetupSequence();
                _messageDispatcherMock.Setup(dispatch => dispatch.Stop()).InSequence(sequence);
                transportMock.Setup(transport => transport.Stop()).InSequence(sequence);
                bus.Configure(_self.Id, "test");

                bus.Start();
                bus.Stop();
                sequence.Verify();
            }

            [Test]
            public void should_start_observing_messages_when_handler_invokes_are_updated()
            {
                // Arrange
                SetupMessageDispatcher();

                // Act
                _messageDispatcherMock.Raise(x => x.MessageHandlerInvokersUpdated += null);

                // Assert
                _directoryMock.Verify(x => x.EnableSubscriptionsUpdatedFor(It.Is<IEnumerable<Type>>(y => y.Single() == typeof(TestMessage))));
            }

            [Test]
            public void should_dispatch_subscriptionsUpdated_messages_when_PeerStarted()
            {
                // Arrange
                SetupMessageDispatcher();
                var (subscription, descriptor) = CaptureDispatchedMessageForRegistration<TestMessage>();

                // Act
                RaisePeerUpdated(descriptor.PeerId, descriptor.Subscriptions);

                // Assert
                _messageDispatcherMock.Verify(x => x.Dispatch(It.IsAny<MessageDispatch>()), Times.Once);
                var subscriptionsUpdated = _dispatchedMessage.Message as SubscriptionsUpdated;
                subscriptionsUpdated.ShouldNotBeNull();
                subscriptionsUpdated.Subscriptions.MessageTypeId.ShouldEqual(subscription.MessageTypeId);
                subscriptionsUpdated.PeerId.ShouldEqual(_otherPeer.Id);
            }

            [Test]
            public void should_not_dispatch_subscriptionsUpdated_messages_when_no_handler_registered()
            {
                // Act
                _messageDispatcherMock.Raise(x => x.MessageHandlerInvokersUpdated += null);

                // Assert
                _directoryMock.Verify(x => x.EnableSubscriptionsUpdatedFor(It.Is<IEnumerable<Type>>(y => !y.Any())));
            }

            [Test]
            public void should_not_dispatch_subscriptionsUpdated_messages_for_different_event()
            {
                // Arrange
                _messageDispatcherMock.Setup(x => x.GetMessageHandlerInvokers()).Returns(new[] { new TestMessageHandlerInvoker<FakeEvent>(), });

                // Act
                _messageDispatcherMock.Raise(x => x.MessageHandlerInvokersUpdated += null);

                // Assert
                _messageDispatcherMock.Verify(x => x.Dispatch(It.IsAny<MessageDispatch>()), Times.Never);
            }

            [Test]
            public void should_not_dispatch_subscriptionUpdated_messages_on_peer_stopped()
            {
                // Arrange
                SetupMessageDispatcher();
                var descriptor = CaptureDispatchedMessage(_otherPeer);

                // Act
                RaisePeerUpdated(descriptor.PeerId, descriptor.Subscriptions);

                // Assert
                _messageDispatcherMock.Verify(x => x.Dispatch(It.IsAny<MessageDispatch>()), Times.Never);
            }

            [Test]
            public void should_not_dispatch_subscriptionUpdated_messages_on_unsubscribe()
            {
                // Arrange
                SetupMessageDispatcher();
                var descriptor = CaptureDispatchedMessage(_otherPeer);

                // Act
                RaisePeerUpdated(descriptor.PeerId, descriptor.Subscriptions);

                // Assert
                _messageDispatcherMock.Verify(x => x.Dispatch(It.IsAny<MessageDispatch>()), Times.Never);
            }

            [Test]
            public void should_dispatch_subscriptionUpdated_messages_on_dynamic_subscription()
            {
                // Arrange
                SetupMessageDispatcher();
                var subscription = new SubscriptionsForType(new MessageTypeId(typeof(TestMessage)), BindingKey.Empty);
                var descriptor = CaptureDispatchedMessage(_otherPeer, subscription.ToSubscriptions());

                // Act
                RaisePeerUpdated(descriptor.PeerId, subscription.ToSubscriptions());

                // Assert
                _messageDispatcherMock.Verify(x => x.Dispatch(It.IsAny<MessageDispatch>()), Times.Once);
                var subscriptionsUpdated = _dispatchedMessage.Message as SubscriptionsUpdated;
                subscriptionsUpdated.ShouldNotBeNull();
                subscriptionsUpdated.Subscriptions.ShouldEqual(subscription);
                subscriptionsUpdated.PeerId.ShouldEqual(_otherPeer.Id);
            }

            [Test]
            public void should_not_dispatch_subscriptionsUpdated_to_self()
            {
                // Arrange
                _messageDispatcherMock.Setup(x => x.GetMessageHandlerInvokers()).Returns(new[] { new NoopMessageHandlerInvoker<TestSubscriptionHandler, SubscriptionsUpdated>() });
                var subscription = new SubscriptionsForType(new MessageTypeId(typeof(TestMessage)), BindingKey.Empty);
                var selfDescriptor = CaptureDispatchedMessage(_self, subscription.ToSubscriptions());
                var otherDescriptor = CaptureDispatchedMessage(_otherPeer, subscription.ToSubscriptions());

                // Act
                RaisePeerUpdated(_self.Id, selfDescriptor.Subscriptions);

                // Assert
                _messageDispatcherMock.Verify(x => x.Dispatch(It.IsAny<MessageDispatch>()), Times.Never);
            }

            private void SetupMessageDispatcher()
            {
                _messageDispatcherMock.Setup(x => x.GetMessageHandlerInvokers()).Returns(new[] { new NoopMessageHandlerInvoker<TestSubscriptionHandler, SubscriptionsUpdated>() });
                _messageDispatcherMock.Raise(x => x.MessageHandlerInvokersUpdated += () => { });
            }

            private void RaisePeerUpdated(PeerId peerId, IReadOnlyList<Subscription> subscriptions) => _directoryMock.Raise(x => x.PeerSubscriptionsUpdated += null, peerId, subscriptions);

            private PeerDescriptor CaptureDispatchedMessage(Peer peer, Subscription[] subscriptions = null)
            {
                _messageDispatcherMock.Setup(x => x.Dispatch(It.IsAny<MessageDispatch>())).Callback<MessageDispatch>(dispatch => { _dispatchedMessage = dispatch; });
                var descriptor = peer.ToPeerDescriptor(true, subscriptions ?? new Subscription[0]);
                return descriptor;
            }

            private (Subscription subscription, PeerDescriptor descriptor) CaptureDispatchedMessageForRegistration<TMessage>(bool shouldInstanciateRegistrationHandlerWithDescriptor = false)
            {
                var subscription = new Subscription(new MessageTypeId(typeof(TMessage)));
                _messageDispatcherMock.Setup(x => x.Dispatch(It.IsAny<MessageDispatch>())).Callback<MessageDispatch>(dispatch => { _dispatchedMessage = dispatch; });
                var descriptor = _otherPeer.ToPeerDescriptor(true, new[] { subscription });
                return (subscription, descriptor);
            }

            class TestMessage : IEvent
            {
            }

            class TestSubscriptionHandler : SubscriptionHandler<TestMessage>
            {
                protected override void OnSubscriptionsUpdated(SubscriptionsForType subscriptions, PeerId peerId)
                {
                }
            }
        }
    }
}
