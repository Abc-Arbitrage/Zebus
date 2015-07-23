using System;
using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Core;
using Abc.Zebus.Directory;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Comparison;
using Abc.Zebus.Testing.Dispatch;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Testing.Transport;
using Abc.Zebus.Testing.UnitTesting;
using Abc.Zebus.Tests.Messages;
using Abc.Zebus.Tests.Serialization;
using Abc.Zebus.Transport;
using Abc.Zebus.Util;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Core
{
    [TestFixture]
    public partial class BusTests
    {
        private const string _deserializationFailureDumpsDirectoryName = "deserialization_failure_dumps";
        private const string _environment = "test";

        private readonly Peer _self = new Peer(new PeerId("Abc.Testing.Self"), "tcp://abctest:123");
        private readonly Peer _peerUp = new Peer(new PeerId("Abc.Testing.Up"), "AnotherPeer");
        private readonly Peer _peerDown = new Peer(new PeerId("Abc.Testing.Down"), "tcp://abctest:999", false);

        private Bus _bus;
        private TestTransport _transport;
        private Mock<IPeerDirectory> _directoryMock;
        private Mock<IMessageDispatcher> _messageDispatcherMock;
        private TestMessageSerializer _messageSerializer;
        private List<IMessageHandlerInvoker> _invokers;
        private string _expectedDumpDirectory;

        [SetUp]
        public void Setup()
        {
            _transport = new TestTransport(_self.EndPoint);
            _directoryMock = new Mock<IPeerDirectory>();
            _messageDispatcherMock = new Mock<IMessageDispatcher>();
            _messageSerializer = new TestMessageSerializer();

            _bus = new Bus(_transport, _directoryMock.Object, _messageSerializer, _messageDispatcherMock.Object, new DefaultStoppingStrategy());
            _bus.Configure(_self.Id, _environment);

            _invokers = new List<IMessageHandlerInvoker>();
            _messageDispatcherMock.Setup(x => x.GetMessageHanlerInvokers()).Returns(_invokers);

            _expectedDumpDirectory = PathUtil.InBaseDirectory(_deserializationFailureDumpsDirectoryName);
            if (System.IO.Directory.Exists(_expectedDumpDirectory))
                System.IO.Directory.Delete(_expectedDumpDirectory, true);

            _directoryMock.Setup(x => x.GetPeersHandlingMessage(It.IsAny<IMessage>())).Returns(new Peer[0]);
        }

        [TearDown]
        public void Teardown()
        {
            if (System.IO.Directory.Exists(_expectedDumpDirectory))
                System.IO.Directory.Delete(_expectedDumpDirectory, true);
        }

        [Test]
        public void should_configure_transport_when_configured()
        {
            var transportMock = new Mock<ITransport>();
            var bus = new Bus(transportMock.Object, new Mock<IPeerDirectory>().Object, null, null, new DefaultStoppingStrategy());

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
            _directoryMock.Setup(x => x.Register(_bus, It.Is<Peer>(p => p.DeepCompare(_self)), It.IsAny<IEnumerable<Subscription>>())).InSequence(sequence);
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
            _directoryMock.Setup(x => x.Register(_bus, It.IsAny<Peer>(), It.IsAny<IEnumerable<Subscription>>()))
                          .Callback(() => wasRunningDuringRegister = _bus.IsRunning);

            _bus.Start();

            wasRunningDuringRegister.ShouldBeTrue();
        }

        [Test]
        public void should_not_be_running_if_registration_failed()
        {
            try
            {
                _directoryMock.Setup(x => x.Register(_bus, It.IsAny<Peer>(), It.IsAny<IEnumerable<Subscription>>()))
                              .Throws<TimeoutException>();
                _bus.Start();
            }
            catch (TimeoutException)
            {
                _bus.IsRunning.ShouldBeFalse();    
            }
        }

        [Test]
        public void should_stop_transport_and_unregister_from_directory()
        {
            var sequence = new SetupSequence();
            _directoryMock.Setup(x => x.Unregister(_bus));

            _bus.Start();
            _bus.Stop();

            sequence.Verify();
            _transport.IsStopped.ShouldBeTrue();
        }

        [Test]
        public void should_stop_message_dispatcher()
        {
            _bus.Start();
            _bus.Stop();

            _messageDispatcherMock.Verify(x => x.Stop());
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

        private void AddInvoker<TMessage>(bool shouldBeSubscribedOnStartup) where TMessage : class, IMessage
        {
            _invokers.Add(new TestMessageHandlerInvoker<TMessage>(shouldBeSubscribedOnStartup));
        }

        private void SetupPeersHandlingMessage<TMessage>(params Peer[] peers) where TMessage : IMessage
        {
            _directoryMock.Setup(x => x.GetPeersHandlingMessage(It.IsAny<TMessage>())).Returns(peers);
        }

        private void SetupDispatch<TMessage>(TMessage message, Action<IMessage> invokerCallback = null, Exception error = null) where TMessage : IMessage
        {
            _messageDispatcherMock.Setup(x => x.Dispatch(It.Is<MessageDispatch>(dispatch => dispatch.Message.DeepCompare(message))))
                                  .Callback<MessageDispatch>(dispatch =>
                                  {
                                      using (MessageContext.SetCurrent(dispatch.Context))
                                      {
                                          if (invokerCallback != null)
                                              invokerCallback(dispatch.Message);

                                          dispatch.SetHandlerCount(1);

                                          var invokerMock = new Mock<IMessageHandlerInvoker>();
                                          invokerMock.SetupGet(x => x.MessageHandlerType).Returns(typeof(FakeMessageHandler));
                                          dispatch.SetHandled(invokerMock.Object, error);
                                      }
                                  });
        }

        private class FakeMessageHandler
        {
        }
    }
}