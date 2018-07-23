using System;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Core;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Directory;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Testing.Transport;
using Abc.Zebus.Util;
using NUnit.Framework;
using ProtoBuf;
using StructureMap;

namespace Abc.Zebus.Tests.Core
{
    [TestFixture]
    public class BusInMemoryTests
    {
        private IBus _bus;
        private TestTransport _transport;
        private Container _container;

        [SetUp]
        public void Setup()
        {
            _transport = new TestTransport();
            _container = new Container();

            _bus = new BusFactory(_container)
                .WithHandlers(typeof(EventPublisherEventHandler))
                .CreateAndStartInMemoryBus(new TestPeerDirectory(), _transport);
        }

        [Test]
        public void should_be_able_to_use_bus_in_handlers_during_shutdown()
        {
            var handler = new EventPublisherEventHandler(_bus);

            _container.Configure(x => x.ForSingletonOf<EventPublisherEventHandler>().Use(handler));
            _transport.RaiseMessageReceived(new EventPublisherEvent().ToTransportMessage());

            // make sure the handler is running
            handler.StartedSignal.WaitOne(5.Seconds()).ShouldBeTrue("Handler was not started");

            var stopTask = Task.Run(() => _bus.Stop()).WaitForActivation();

            // resume the handler
            handler.WaitingSignal.Set();

            // wait for handler completion
            Wait.Until(() => handler.Processed, 5.Seconds(), "Handler was not processed");

            handler.Error.ShouldBeNull("Handler was not able to send a message");

            stopTask.Wait(10.Seconds()).ShouldBeTrue("Bus was not stopped");
        }

        [ProtoContract]
        public class Event : IEvent
        {
        }

        [ProtoContract]
        public class EventPublisherEvent : IEvent
        {
        }

        public class EventPublisherEventHandler : IMessageHandler<EventPublisherEvent>
        {
            private readonly IBus _bus;

            public EventPublisherEventHandler(IBus bus)
            {
                _bus = bus;

                StartedSignal = new ManualResetEvent(false);
                WaitingSignal = new ManualResetEvent(false);
            }

            public EventWaitHandle StartedSignal { get; private set; }
            public EventWaitHandle WaitingSignal { get; private set; }
            public Exception Error { get; private set; }
            public bool Processed { get; private set; }

            public void Handle(EventPublisherEvent message)
            {
                StartedSignal.Set();
                WaitingSignal.WaitOne();
                try
                {
                    _bus.Publish(new Event());
                }
                catch (Exception ex)
                {
                    Error = ex;
                }
                finally
                {
                    Processed = true;
                }
            }
        }
    }
}
