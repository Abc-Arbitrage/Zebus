using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Core;
using Abc.Zebus.Directory;
using Abc.Zebus.Routing;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Comparison;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Testing.UnitTesting;
using Abc.Zebus.Tests.Messages;
using Abc.Zebus.Transport;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Core
{
    public partial class BusTests
    {
        public class Core : BusTests
        {
            [Test]
            public void should_configure_transport_when_configured()
            {
                var transportMock = new Mock<ITransport>();
                var bus = new Bus(transportMock.Object, new Mock<IPeerDirectory>().Object, null, null, new DefaultMessageSendingStrategy(), new DefaultStoppingStrategy(), Mock.Of<IBindingKeyPredicateBuilder>(), _configuration.Object);

                bus.Configure(_self.Id, _environment);

                transportMock.Verify(trans => trans.Configure(It.Is<PeerId>(peerId => _self.Id.Equals(peerId)), _environment));
            }

            [Test]
            public void should_initialize_transport_and_register_on_directory()
            {
                AddInvoker<FakeCommand>(shouldBeSubscribedOnStartup: true);

                var sequence = new SetupSequence();

                _messageDispatcherMock.Setup(x => x.LoadMessageHandlerInvokers()).InSequence(sequence);
                _transport.Started += sequence.GetCallback();
                _directoryMock.Setup(x => x.RegisterAsync(_bus, It.Is<Peer>(p => p.DeepCompare(_self)), It.IsAny<IEnumerable<Subscription>>()))
                              .InSequence(sequence)
                              .Returns(Task.CompletedTask);
                _transport.Registered += sequence.GetCallback();

                _bus.Start();

                sequence.Verify();
                _transport.IsStarted.ShouldBeTrue();
                _transport.IsRegistered.ShouldBeTrue();
            }

            [Test]
            public void should_be_running_when_registering_on_directory()
            {
                var wasRunningDuringRegister = false;
                _directoryMock.Setup(x => x.RegisterAsync(_bus, It.IsAny<Peer>(), It.IsAny<IEnumerable<Subscription>>()))
                              .Callback(() => wasRunningDuringRegister = _bus.IsRunning)
                              .Returns(Task.CompletedTask);

                _bus.Start();

                wasRunningDuringRegister.ShouldBeTrue();
            }

            [Test]
            public void should_not_be_running_if_registration_failed()
            {
                try
                {
                    _directoryMock.Setup(x => x.RegisterAsync(_bus, It.IsAny<Peer>(), It.IsAny<IEnumerable<Subscription>>()))
                                  .Returns(Task.FromException(new TimeoutException()));
                    _bus.Start();
                }
                catch (AggregateException ex) when (ex.InnerException is TimeoutException)
                {
                    _bus.IsRunning.ShouldBeFalse();
                }
            }

            [Test]
            public void should_stop_transport_and_unregister_from_directory()
            {
                var sequence = new SetupSequence();
                _directoryMock.Setup(x => x.UnregisterAsync(_bus)).Returns(Task.CompletedTask);

                _bus.Start();
                _bus.Stop();

                sequence.Verify();
                _transport.IsStopped.ShouldBeTrue();
            }

            [Test]
            public void should_raise_started_but_not_delivering_messages_event_when_starting()
            {
                var raised = false;
                _bus.StartedButNotDeliveringMessages += () => raised = true;

                _bus.Start();

                raised.ShouldBeTrue();
            }

            [Test]
            public void should_not_start_message_dispatcher_until_started_but_not_delivering_messages_event_is_handled()
            {
                _bus.StartedButNotDeliveringMessages += () => _messageDispatcherMock.Verify(x => x.StartDeliveringMessages(), Times.Never);

                _bus.Start();
            }

            [Test]
            public void should_stop_message_dispatcher()
            {
                _bus.Start();
                _bus.Stop();

                _messageDispatcherMock.Verify(x => x.Stop());
            }

            [Test]
            public void should_stop_message_dispatcher_when_directory_unsubscription_fails()
            {
                _directoryMock.Setup(i => i.UnregisterAsync(It.IsAny<IBus>()))
                              .Returns(Task.FromException(new InvalidOperationException()));

                _bus.Start();
                _bus.Stop();

                _messageDispatcherMock.Verify(i => i.Stop());
            }

            [Test]
            public void should_fail_when_starting_started_bus()
            {
                _bus.Start();

                var exception = Assert.Throws<InvalidOperationException>(() => _bus.Start());
                exception.Message.ShouldContain("already running");
            }

            [Test]
            public void should_fail_when_stopping_non_started_bus()
            {
                var exception = Assert.Throws<InvalidOperationException>(() => _bus.Stop());
                exception.Message.ShouldContain("not running");
            }

            [Test]
            public void should_fire_events_starting_and_started_when_calling_start()
            {
                var startingEventCalled = 0;
                var startedEventCalled = 0;
                _bus.Starting += () => startingEventCalled = 1;
                _bus.Started += () => startedEventCalled = startingEventCalled + 1;

                _bus.Start();
                _bus.Stop();

                startingEventCalled.ShouldEqual(1);
                startedEventCalled.ShouldEqual(2);
            }

            [Test]
            public void should_fire_event_stopping_and_stopped_when_calling_Stop()
            {
                var stoppingEventCalled = 0;
                var stoppedEventCalled = 0;
                _bus.Stopping += () => stoppingEventCalled = 1;
                _bus.Stopped += () => stoppedEventCalled = stoppingEventCalled + 1;

                _bus.Start();
                _bus.Stop();

                stoppingEventCalled.ShouldEqual(1);
                stoppedEventCalled.ShouldEqual(2);
            }

            [Test]
            public void should_start_message_dispatcher()
            {
                _bus.Start();

                _messageDispatcherMock.Verify(x => x.Start());
            }

            [Test]
            public void should_forward_initiator_id()
            {
                _bus.Start();

                using (MessageId.PauseIdGeneration())
                {
                    var receivedCommand = new FakeCommand(123);
                    var eventToPublish = new FakeEvent(456);
                    SetupDispatch(receivedCommand, _ => _bus.Publish(eventToPublish));
                    SetupPeersHandlingMessage<FakeEvent>(_peerDown);

                    using (MessageContext.SetCurrent(MessageContext.CreateTest(new OriginatorInfo(_peerUp.Id, _peerUp.EndPoint, null, "x.initiator"))))
                    {
                        var transportMessageReceived = receivedCommand.ToTransportMessage(_peerUp);
                        transportMessageReceived.Originator.InitiatorUserName.ShouldEqual("x.initiator");
                        _transport.RaiseMessageReceived(transportMessageReceived);
                    }

                    var sentMessage = _transport.Messages.Single(x => x.TransportMessage.MessageTypeId == eventToPublish.TypeId());
                    sentMessage.TransportMessage.Originator.InitiatorUserName.ShouldEqual("x.initiator");
                }
            }

            [TestCase(PeerUpdateAction.Started)]
            [TestCase(PeerUpdateAction.Updated)]
            public void should_forward_peer_update_to_transport(PeerUpdateAction updateAction)
            {
                _directoryMock.Raise(x => x.PeerUpdated += null, _peerUp.Id, updateAction);

                var updatedPeer = _transport.UpdatedPeers.ExpectedSingle();
                updatedPeer.PeerId.ShouldEqual(_peerUp.Id);
                updatedPeer.UpdateAction.ShouldEqual(updateAction);
            }

            [Test]
            public void should_set_the_peerId_property()
            {
                _bus.PeerId.ShouldEqual(_self.Id);
            }

            [Test]
            public void should_set_the_environment_property()
            {
                _bus.Environment.ShouldEqual(_environment);
            }

            [Test]
            public void should_stop_if_starting_event_throws()
            {
                _bus.Starting += () => throw new DivideByZeroException();
                Assert.Throws<DivideByZeroException>(() => _bus.Start());
                _bus.IsRunning.ShouldBeFalse();
            }

            [Test]
            public void should_continue_running_if_stopping_event_throws()
            {
                _bus.Stopping += () => throw new DivideByZeroException();
                _bus.Start();
                Assert.Throws<DivideByZeroException>(() => _bus.Stop());
                _bus.IsRunning.ShouldBeTrue();
            }
        }
    }
}
