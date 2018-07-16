using System;
using System.Threading.Tasks;
using Abc.Zebus.Core;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Messages;
using Abc.Zebus.Util;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Core
{
    [TestFixture]
    public class MessageContextAwareBusTests
    {
        private MessageContextAwareBus _bus;
        private Mock<IBus> _busMock;
        private MessageContext _context;

        [SetUp]
        public void Setup()
        {
            _busMock = new Mock<IBus>();
            _context = MessageContext.CreateTest("u.name");

            _bus = new MessageContextAwareBus(_busMock.Object, _context);
        }

        [Test]
        public void should_get_peer_id()
        {
            var peerId = new PeerId("Abc.Foo.0");
            _busMock.SetupGet(x => x.PeerId).Returns(peerId);

            _bus.PeerId.ShouldEqual(peerId);
        }

        [Test]
        public void should_configure_bus()
        {
            var peerId = new PeerId("Abc.Foo.0");
            var environment = "Dev";

            _bus.Configure(peerId, environment);

            _busMock.Verify(x => x.Configure(peerId, environment));
        }

        [Test]
        public void should_publish_event_with_message_context()
        {
            MessageContext.Current.ShouldBeNull();

            var message = new FakeEvent(1);
            MessageContext context = null;
            _busMock.Setup(x => x.Publish(message)).Callback(() => context = MessageContext.Current);

            _bus.Publish(message);

            MessageContext.Current.ShouldBeNull();
            context.ShouldEqual(_context);
        }

        [Test]
        public void should_send_command_with_message_context()
        {
            MessageContext.Current.ShouldBeNull();

            var message = new FakeCommand(1);
            MessageContext context = null;
            _busMock.Setup(x => x.Send(message)).Callback(() => context = MessageContext.Current);

            _bus.Send(message);

            MessageContext.Current.ShouldBeNull();
            context.ShouldEqual(_context);
        }

        [Test]
        public void should_send_command_to_peer_with_message_context()
        {
            MessageContext.Current.ShouldBeNull();

            var message = new FakeCommand(1);
            var peer = new Peer(new PeerId("Abc.Foo.0"), "tcp://dtc:1234");
            MessageContext context = null;
            _busMock.Setup(x => x.Send(message, peer)).Callback(() => context = MessageContext.Current);

            _bus.Send(message, peer);

            MessageContext.Current.ShouldBeNull();
            context.ShouldEqual(_context);
        }

        [Test]
        public void should_subscribe()
        {
            var expectedScope = new DisposableAction(() => { });
            var subscription =  new SubscriptionRequest(new Subscription(MessageUtil.TypeId<FakeCommand>()));
            _busMock.Setup(x => x.SubscribeAsync(subscription)).Returns(Task.FromResult<IDisposable>(expectedScope));

            var scope = _bus.SubscribeAsync(subscription);

            scope.Result.ShouldEqual(expectedScope);
        }

        [Test]
        public void should_reply_with_code()
        {
            _bus.Reply(42);

            _context.ReplyCode.ShouldEqual(42);
        }

        [Test]
        public void should_reply_with_response()
        {
            var response = new FakeCommandResult("X", 42);

            _bus.Reply(response);

            _context.ReplyResponse.ShouldEqual(response);
        }

        [Test]
        public void should_start_bus()
        {
            _bus.Start();

            _busMock.Verify(x => x.Start());
        }

        [Test]
        public void should_stop_bus()
        {
            _bus.Stop();

            _busMock.Verify(x => x.Stop());
        }
    }
}