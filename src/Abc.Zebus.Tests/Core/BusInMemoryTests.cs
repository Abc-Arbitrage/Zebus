using System;
using System.Linq;
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
using Lamar;
using Microsoft.Extensions.DependencyInjection;

namespace Abc.Zebus.Tests.Core
{
    [TestFixture]
    public class BusInMemoryTests
    {
        private IBus _bus;
        private TestTransport _transport;
        private Container _container;
        private TestPeerDirectory _peerDirectory;

        [SetUp]
        public void Setup()
        {
            _transport = new TestTransport();
            _container = new Container(new ServiceRegistry());
            _peerDirectory = new TestPeerDirectory();

            _bus = new BusFactory(_container)
                .WithHandlers(typeof(EventPublisherEventHandler), typeof(CommandHandleThatReplyAndThrow))
                .CreateAndStartInMemoryBus(_peerDirectory, _transport);
        }

        [Test]
        public void should_be_able_to_use_bus_in_handlers_during_shutdown()
        {
            var handler = new EventPublisherEventHandler(_bus);

            _container.Configure(x => x.AddSingleton<EventPublisherEventHandler>(handler));
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

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void should_publish_event_to_peers(int peerCount)
        {
            for (var i = 0; i < peerCount; i++)
            {
                var peer = TestData.Peer();
                _peerDirectory.Peers[peer.Id] = peer.ToPeerDescriptor(false, typeof(Event));
            }

            _bus.Publish(new Event());

            _transport.Messages.SelectMany(x => x.Targets).Count().ShouldEqual(peerCount);
        }

        [Test]
        public async Task should_preserve_reply_object_on_error()
        {
            var response = await _bus.Send(new Command());

            response.IsSuccess.ShouldBeFalse();
            response.Response.ShouldNotBeNull();
            response.Response.ShouldEqualDeeply(new CommandResponse { Id = 42 });
        }

        [ProtoContract]
        public class Command : ICommand
        {
        }

        [ProtoContract]
        public class CommandResponse : IMessage
        {
            [ProtoMember(1)]
            public int Id { get; set; }
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

        public class CommandHandleThatReplyAndThrow : IMessageHandler<Command>
        {
            private readonly IBus _bus;

            public CommandHandleThatReplyAndThrow(IBus bus)
            {
                _bus = bus;
            }

            public void Handle(Command message)
            {
                _bus.Reply(new CommandResponse { Id = 42 });

                throw new Exception("By design");
            }
        }
    }
}
