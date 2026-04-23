using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Core;
using Abc.Zebus.Directory;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Monitoring;
using Abc.Zebus.Serialization;
using Abc.Zebus.Testing.Dispatch;
using Abc.Zebus.Testing.Transport;
using Abc.Zebus.Transport;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Monitoring
{
    [TestFixture]
    public class ZebusMetricsTests
    {
        private const string _environment = "Test";

        private readonly Peer _self = new(new PeerId("Abc.Testing.Self"), "tcp://abctest:123");

        private Bus _bus;
        private TestTransport _transport;
        private Mock<IPeerDirectory> _directoryMock;
        private Mock<IMessageDispatcher> _messageDispatcherMock;
        private MeterListener _meterListener;
        private Dictionary<string, long> _longMeasurements;
        private Dictionary<string, double> _doubleMeasurements;

        [SetUp]
        public void Setup()
        {
            _transport = new TestTransport(_self.EndPoint);
            _directoryMock = new Mock<IPeerDirectory>();
            _messageDispatcherMock = new Mock<IMessageDispatcher>();

            _bus = new Bus(
                _transport,
                _directoryMock.Object,
                new MessageSerializer(),
                _messageDispatcherMock.Object,
                new DefaultMessageSendingStrategy(),
                new DefaultStoppingStrategy(),
                new BusConfiguration("tcp://zebus-directory:123")
            );
            _bus.Configure(_self.Id, _environment);

            _messageDispatcherMock.Setup(x => x.GetMessageHandlerInvokers()).Returns(new List<IMessageHandlerInvoker>());

            _longMeasurements = new Dictionary<string, long>();
            _doubleMeasurements = new Dictionary<string, double>();

            _meterListener = new MeterListener();
            _meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == ZebusMetrics.MeterName)
                    listener.EnableMeasurementEvents(instrument);
            };
            _meterListener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
            {
                _longMeasurements.TryGetValue(instrument.Name, out var current);
                _longMeasurements[instrument.Name] = current + measurement;
            });
            _meterListener.SetMeasurementEventCallback<double>((instrument, measurement, _, _) =>
            {
                _doubleMeasurements.TryGetValue(instrument.Name, out var current);
                _doubleMeasurements[instrument.Name] = current + measurement;
            });
            _meterListener.Start();
        }

        [TearDown]
        public void Teardown()
        {
            _meterListener.Dispose();
        }

        [Test]
        public void should_record_bus_started_metric()
        {
            _directoryMock.Setup(x => x.RegisterAsync(_bus, It.IsAny<Peer>(), It.IsAny<IEnumerable<Subscription>>()))
                          .Returns(Task.CompletedTask);

            _bus.Start();
            _meterListener.RecordObservableInstruments();

            Assert.That(_longMeasurements.GetValueOrDefault("zebus.bus.started"), Is.EqualTo(1));

            _bus.Stop();
        }

        [Test]
        public void should_record_bus_stopped_metric()
        {
            _directoryMock.Setup(x => x.RegisterAsync(_bus, It.IsAny<Peer>(), It.IsAny<IEnumerable<Subscription>>()))
                          .Returns(Task.CompletedTask);

            _bus.Start();
            _bus.Stop();
            _meterListener.RecordObservableInstruments();

            Assert.That(_longMeasurements.GetValueOrDefault("zebus.bus.stopped"), Is.EqualTo(1));
        }

        [Test]
        public void should_record_message_sent_metric()
        {
            _directoryMock.Setup(x => x.RegisterAsync(_bus, It.IsAny<Peer>(), It.IsAny<IEnumerable<Subscription>>()))
                          .Returns(Task.CompletedTask);

            var peerUp = new Peer(new PeerId("Abc.Testing.Up"), "tcp://abctest:456");
            _directoryMock.Setup(x => x.GetPeersHandlingMessage(It.IsAny<IMessage>())).Returns(new[] { peerUp });

            _bus.Start();
            _bus.Publish(new FakeEvent());
            _meterListener.RecordObservableInstruments();

            Assert.That(_longMeasurements.GetValueOrDefault("zebus.messages.sent"), Is.EqualTo(1));

            _bus.Stop();
        }

        [Test]
        public void should_record_message_received_metric()
        {
            _directoryMock.Setup(x => x.RegisterAsync(_bus, It.IsAny<Peer>(), It.IsAny<IEnumerable<Subscription>>()))
                          .Returns(Task.CompletedTask);

            _bus.Start();

            var transportMessage = new TransportMessage(new MessageTypeId(typeof(FakeEvent)), default, new PeerId("Abc.Testing.Sender"), "tcp://sender:123");
            _transport.RaiseMessageReceived(transportMessage);
            _meterListener.RecordObservableInstruments();

            Assert.That(_longMeasurements.GetValueOrDefault("zebus.messages.received"), Is.EqualTo(1));

            _bus.Stop();
        }

        [Test]
        public void should_record_peer_updated_metric()
        {
            _directoryMock.Setup(x => x.RegisterAsync(_bus, It.IsAny<Peer>(), It.IsAny<IEnumerable<Subscription>>()))
                          .Returns(Task.CompletedTask);

            _bus.Start();

            _directoryMock.Raise(x => x.PeerUpdated += null, new PeerId("Abc.Testing.Up"), PeerUpdateAction.Started);
            _meterListener.RecordObservableInstruments();

            Assert.That(_longMeasurements.GetValueOrDefault("zebus.directory.peer_updates"), Is.EqualTo(1));

            _bus.Stop();
        }

        [Test]
        public void should_expose_meter_with_expected_name()
        {
            Assert.That(ZebusMetrics.MeterName, Is.EqualTo("Abc.Zebus"));
        }

        [ProtoBuf.ProtoContract]
        private class FakeEvent : IEvent
        {
        }
    }
}
