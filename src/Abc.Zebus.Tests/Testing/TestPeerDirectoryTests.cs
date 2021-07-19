using System.Collections.Generic;
using System.Threading.Tasks;
using Abc.Zebus.Directory;
using Abc.Zebus.Routing;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Directory;
using Abc.Zebus.Testing.Extensions;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Testing
{
    [TestFixture]
    public class TestPeerDirectoryTests
    {
        private TestPeerDirectory _peerDirectory;
        private Peer _self;
        private TestBus _bus;

        [SetUp]
        public void SetUp()
        {
            _peerDirectory = new TestPeerDirectory();
            _self = new Peer(new PeerId("Abc.Testing.Self.0"), "tcp://myself:123");
            _bus = new TestBus();

            _bus.Configure(_self.Id);
        }

        [Test]
        public async Task should_add_new_subscriptions()
        {
            // Arrange
            await _peerDirectory.RegisterAsync(_bus, _self, new List<Subscription>());

            // Act
            var subscriptionsForTypes = new List<SubscriptionsForType>
            {
                new(MessageUtil.TypeId<Message1>(), BindingKey.Empty),
            };

            await _peerDirectory.UpdateSubscriptionsAsync(_bus, subscriptionsForTypes);

            // Assert
            var subscriptions = _peerDirectory.GetSelfSubscriptions();
            subscriptions.ShouldBeEquivalentTo(new []
            {
                Subscription.Any<Message1>(),
            });
        }

        [Test]
        public async Task should_add_and_merge_new_subscriptions()
        {
            // Arrange
            await _peerDirectory.RegisterAsync(_bus, _self, new List<Subscription>
            {
                new(MessageUtil.TypeId<Message1>(), BindingKey.Empty),
                new(MessageUtil.TypeId<Message2>(), new BindingKey("1")),
            });

            // Act
            var subscriptionsForTypes = new List<SubscriptionsForType>
            {
                new(MessageUtil.TypeId<Message2>(), new BindingKey("2")),
            };

            await _peerDirectory.UpdateSubscriptionsAsync(_bus, subscriptionsForTypes);

            // Assert
            var subscriptions = _peerDirectory.GetSelfSubscriptions();
            subscriptions.ShouldBeEquivalentTo(new Subscription[]
            {
                new(MessageUtil.TypeId<Message1>(), BindingKey.Empty),
                new(MessageUtil.TypeId<Message2>(), new BindingKey("1")),
                new(MessageUtil.TypeId<Message2>(), new BindingKey("2")),
            });
        }

        [Test]
        public async Task should_update_and_merge_new_subscriptions()
        {
            // Arrange
            await _peerDirectory.RegisterAsync(_bus, _self, new List<Subscription>
            {
                new(MessageUtil.TypeId<Message2>(), new BindingKey("1")),
            });

            var update1 = new List<SubscriptionsForType>
            {
                new(MessageUtil.TypeId<Message2>(), new BindingKey("2")),
            };

            await _peerDirectory.UpdateSubscriptionsAsync(_bus, update1);

            // Act
            var update2 = new List<SubscriptionsForType>
            {
                new(MessageUtil.TypeId<Message2>(), new BindingKey("3")),
            };

            await _peerDirectory.UpdateSubscriptionsAsync(_bus, update2);

            // Assert
            var subscriptions = _peerDirectory.GetSelfSubscriptions();
            subscriptions.ShouldBeEquivalentTo(new Subscription[]
            {
                new(MessageUtil.TypeId<Message2>(), new BindingKey("1")),
                new(MessageUtil.TypeId<Message2>(), new BindingKey("3")),
            });
        }

        private class Message1 : IEvent
        {
        }

        [Routable]
        private class Message2 : IEvent
        {
            [RoutingPosition(1)]
            public int Key { get; set; }
        }
    }
}
