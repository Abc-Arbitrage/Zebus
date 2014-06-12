using System.Linq;
using Abc.Zebus.Directory.Tests;
using Abc.Zebus.Routing;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util;
using Cassandra;
using Cassandra.Data.Linq;
using NUnit.Framework;
using System;

namespace Abc.Zebus.Directory.Cassandra.Tests.Storage
{
    public partial class CqlPeerRepositoryTests
    {
        [Test]
        public void should_add_dynamic_subscriptions()
        {
            var peerDescriptor = _peer1.ToPeerDescriptorWithRoundedTime(true, typeof(FakeCommand));
            _repository.AddOrUpdatePeer(peerDescriptor);

            _repository.AddDynamicSubscriptions(peerDescriptor, new[] { CreateSubscriptionFor<int>() });

            var fetched = _repository.Get(peerDescriptor.Peer.Id);
            fetched.Subscriptions.ShouldEqual(new[]
            {
                CreateSubscriptionFor<FakeCommand>(),
                CreateSubscriptionFor<int>()
            });
        }

        private Subscription CreateSubscriptionFor<TMessage>(params string[] bindingKeyParts)
        {
            return new Subscription(MessageUtil.GetTypeId(typeof(TMessage)), new BindingKey(bindingKeyParts));
        }

        [Test]
        public void should_remove_dynamic_subscriptions()
        {
            var peerDescriptor = _peer1.ToPeerDescriptorWithRoundedTime(true, typeof(FakeCommand));
            _repository.AddOrUpdatePeer(peerDescriptor);
            _repository.AddDynamicSubscriptions(peerDescriptor, new[] { CreateSubscriptionFor<int>() });

            _repository.RemoveDynamicSubscriptions(peerDescriptor, new[] { CreateSubscriptionFor<int>() });

            var fetched = _repository.Get(peerDescriptor.Peer.Id);
            fetched.ShouldHaveSamePropertiesAs(peerDescriptor);
        }

        [Test]
        public void should_remove_the_dynamic_subscriptions_of_a_peer_when_removing_it()
        {
            var peerDescriptor = _peer1.ToPeerDescriptorWithRoundedTime(true, typeof(FakeCommand));
            _repository.AddOrUpdatePeer(peerDescriptor);
			_repository.AddDynamicSubscriptions(peerDescriptor, new[] { CreateSubscriptionFor<int>() });

			_repository.RemovePeer(peerDescriptor.PeerId);

            _repository.Get(peerDescriptor.PeerId).ShouldBeNull();
            var retrievedSubscriptions = DataContext.DynamicSubscriptions
                                                    .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                                                    .Where(sub => sub.PeerId == peerDescriptor.PeerId.ToString())
                                                    .Execute();
			retrievedSubscriptions.ShouldBeEmpty();
        }

        [Test]
		public void should_not_add_dynamic_subscriptions_using_outdated_add_command()
        {
            var pastPeerDescriptor = _peer1.ToPeerDescriptorWithRoundedTime(true, typeof(FakeCommand));
			pastPeerDescriptor.TimestampUtc = SystemDateTime.UtcNow.AddMinutes(-1).RoundToMillisecond();
            var presentPeerDescriptor = _peer1.ToPeerDescriptorWithRoundedTime(true, typeof(FakeCommand));
			_repository.AddOrUpdatePeer(presentPeerDescriptor);

            _repository.RemoveDynamicSubscriptions(presentPeerDescriptor, new[] { CreateSubscriptionFor<int>() });
			_repository.AddDynamicSubscriptions(pastPeerDescriptor, new[] { CreateSubscriptionFor<int>() });

            var fetched = _repository.Get(presentPeerDescriptor.Peer.Id);
            fetched.Subscriptions.ShouldEqual(new[] { CreateSubscriptionFor<FakeCommand>() });
        }

        [Test]
        public void should_not_remove_dynamic_subscriptions_using_outdated_delete_command()
        {
            var pastPeerDescriptor = _peer1.ToPeerDescriptorWithRoundedTime(true, typeof(FakeCommand));
            pastPeerDescriptor.TimestampUtc = SystemDateTime.UtcNow.AddMinutes(-1).RoundToMillisecond();
            var presentPeerDescriptor = _peer1.ToPeerDescriptorWithRoundedTime(true, typeof(FakeCommand));
            _repository.AddOrUpdatePeer(presentPeerDescriptor);
            _repository.AddDynamicSubscriptions(presentPeerDescriptor, new[] { CreateSubscriptionFor<int>() });

            _repository.AddDynamicSubscriptions(presentPeerDescriptor, new[] { CreateSubscriptionFor<int>() });
            _repository.RemoveDynamicSubscriptions(pastPeerDescriptor, new[] { CreateSubscriptionFor<int>() });
            

            var fetched = _repository.Get(presentPeerDescriptor.Peer.Id);
            fetched.Subscriptions.ShouldEqual(new[]
            {
                CreateSubscriptionFor<FakeCommand>(),
                CreateSubscriptionFor<int>()
            });
        }

        [Test]
        public void should_get_a_peer_with_no_subscriptions_using_GetPeers()
        {
            var peerDescriptor = _peer1.ToPeerDescriptorWithRoundedTime(true);

            _repository.AddOrUpdatePeer(peerDescriptor);

            var peerFetched = _repository.GetPeers().ExpectedSingle();
            peerFetched.ShouldHaveSamePropertiesAs(peerDescriptor);
        }

        [Test]
        public void parts_should_stay_in_order()
        {
            var peerDescriptor = _peer1.ToPeerDescriptorWithRoundedTime(true);
            var random = new Random();
            var bindingKeyTokens = Enumerable.Range(1, 100).Select(i => random.Next().ToString()).ToArray();
            _repository.AddOrUpdatePeer(peerDescriptor);

            _repository.AddDynamicSubscriptions(peerDescriptor, new[] { CreateSubscriptionFor<int>(bindingKeyTokens) });

            var fetched = _repository.Get(peerDescriptor.Peer.Id);
            fetched.Subscriptions.ShouldEqual(new[] { CreateSubscriptionFor<int>(bindingKeyTokens) });
        }

        [Test]
        public void removing_a_dynamic_subscription_doesnt_remove_static_subscription()
        {
            var peerDescriptor = _peer1.ToPeerDescriptorWithRoundedTime(true, typeof(FakeCommand));
            _repository.AddOrUpdatePeer(peerDescriptor);

            _repository.RemoveDynamicSubscriptions(peerDescriptor, new[] { CreateSubscriptionFor<FakeCommand>() });

            var fetched = _repository.Get(peerDescriptor.Peer.Id);
            fetched.ShouldHaveSamePropertiesAs(peerDescriptor);
        }

        [Test]
        public void should_deduplicate_dynamic_subscriptions_and_static_subscriptions()
        {
            var peerDescriptor = _peer1.ToPeerDescriptorWithRoundedTime(true, typeof(FakeCommand));
            _repository.AddOrUpdatePeer(peerDescriptor);

            _repository.AddDynamicSubscriptions(peerDescriptor, new[] { CreateSubscriptionFor<FakeCommand>(), CreateSubscriptionFor<FakeCommand>() });

            var fetched = _repository.Get(peerDescriptor.Peer.Id);
            fetched.Subscriptions.ShouldEqual(new[] { CreateSubscriptionFor<FakeCommand>() });
        }

		[Test]
        public void should_not_mixup_subscriptions_to_same_type_with_different_tokens()
        {
            var peerDescriptor = _peer1.ToPeerDescriptorWithRoundedTime(true, typeof(FakeCommand));
            _repository.AddOrUpdatePeer(peerDescriptor);

            _repository.AddDynamicSubscriptions(peerDescriptor, new[] { CreateSubscriptionFor<FakeCommand>("bli"), CreateSubscriptionFor<FakeCommand>("bla") });

            var fetched = _repository.Get(peerDescriptor.Peer.Id);
            fetched.Subscriptions.ShouldEqual(new[]
            {
                CreateSubscriptionFor<FakeCommand>(),
                CreateSubscriptionFor<FakeCommand>("bli"),
                CreateSubscriptionFor<FakeCommand>("bla")
            });
        }

        [Test]
        public void should_not_erase_the_dynamic_subscriptions_of_a_peer_on_update()
        {
            var peerDescriptor = _peer1.ToPeerDescriptorWithRoundedTime(true, typeof(FakeCommand));
            _repository.AddDynamicSubscriptions(peerDescriptor, new[] { CreateSubscriptionFor<int>() });

            _repository.AddOrUpdatePeer(peerDescriptor);

            var expectedPeerDescriptor = new PeerDescriptor(peerDescriptor);
            expectedPeerDescriptor.Subscriptions = new[] { CreateSubscriptionFor<FakeCommand>(), CreateSubscriptionFor<int>() };
            var peerFetched = _repository.Get(peerDescriptor.Peer.Id);
            peerFetched.ShouldHaveSamePropertiesAs(expectedPeerDescriptor);
        }

        [Test]
        public void should_get_dynamic_subscriptions_in_GetPeers()
        {
            var peerDescriptor = _peer1.ToPeerDescriptorWithRoundedTime(true, typeof(FakeCommand));
            _repository.AddOrUpdatePeer(peerDescriptor);

            _repository.AddDynamicSubscriptions(peerDescriptor, new[] { CreateSubscriptionFor<int>() });

            var fetched = _repository.GetPeers().ExpectedSingle();
            fetched.Subscriptions.ShouldEqual(new[]
            {
                CreateSubscriptionFor<FakeCommand>(),
                CreateSubscriptionFor<int>()
            });
        }

    }
}

