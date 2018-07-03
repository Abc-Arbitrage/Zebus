using System;
using System.Linq;
using Abc.Zebus.Directory.Storage;
using Abc.Zebus.Routing;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util;
using NUnit.Framework;

namespace Abc.Zebus.Directory.Tests.Storage
{
    [TestFixture]
    public class MemoryPeerRepositoryTests
    {
        private MemoryPeerRepository _repository;
        private Peer _peer1;
        private Peer _peer2;

        [SetUp]
        public void Setup()
        {
            _repository = new MemoryPeerRepository();
            _peer1 = new Peer(new PeerId("Abc.Peer.1"), "tcp://endpoint:123");
            _peer2 = new Peer(new PeerId("Abc.Peer.2"), "tcp://endpoint:456");
        }

        [Test]
        public void should_insert_and_get_peer()
        {
            var peerDescriptor = _peer1.ToPeerDescriptor(true, typeof(FakeCommand));

            _repository.AddOrUpdatePeer(peerDescriptor);

            var fetched = _repository.Get(peerDescriptor.Peer.Id);
            fetched.ShouldHaveSamePropertiesAs(peerDescriptor);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void get_persistent_state(bool isPersistent)
        {
            _repository.AddOrUpdatePeer(_peer1.ToPeerDescriptor(isPersistent));

            _repository.IsPersistent(_peer1.Id).ShouldEqual(isPersistent);
        }

        [Test]
        public void get_persistent_state_when_peer_does_not_exists()
        {
            _repository.IsPersistent(_peer1.Id).ShouldBeNull();
        }

        [Test]
        public void should_return_null_when_peer_does_not_exists()
        {
            var fetched = _repository.Get(new PeerId("PeerId"));

            fetched.ShouldBeNull();
        }

        [Test]
        public void should_get_all_peers()
        {
            var peerDescriptor1 = _peer1.ToPeerDescriptor(true, typeof(string));
            peerDescriptor1.HasDebuggerAttached = true;
            _repository.AddOrUpdatePeer(peerDescriptor1);

            var peerDescriptor2 = _peer2.ToPeerDescriptor(true, typeof(int));
            _repository.AddOrUpdatePeer(peerDescriptor2);

            var fetchedPeers = _repository.GetPeers().ToList();

            var fetchedPeer1 = fetchedPeers.Single(x => x.Peer.Id == peerDescriptor1.Peer.Id);
            fetchedPeer1.ShouldHaveSamePropertiesAs(peerDescriptor1);
            var fetchedPeer2 = fetchedPeers.Single(x => x.Peer.Id == peerDescriptor2.Peer.Id);
            fetchedPeer2.ShouldHaveSamePropertiesAs(peerDescriptor2);
        }

        [Test]
        public void should_update_peer()
        {
            var peerDescriptor = _peer1.ToPeerDescriptor(true, typeof(string));
            _repository.AddOrUpdatePeer(peerDescriptor);

            var updatedPeer = _peer1.ToPeerDescriptor(false, typeof(int));
            _repository.AddOrUpdatePeer(updatedPeer);

            var fetchedPeers = _repository.GetPeers();
            var fetchedPeer = fetchedPeers.Single();
            fetchedPeer.ShouldHaveSamePropertiesAs(updatedPeer);
        }

        [Test]
        public void should_not_override_peer_with_old_version()
        {
            var descriptor1 = _peer1.ToPeerDescriptor(true, typeof(FakeCommand));
            descriptor1.TimestampUtc = SystemDateTime.UtcNow.AddMinutes(1);
            _repository.AddOrUpdatePeer(descriptor1);

            var descriptor2 = _peer1.ToPeerDescriptor(true);
            _repository.AddOrUpdatePeer(descriptor2);

            var fetched = _repository.Get(_peer1.Id);
            fetched.TimestampUtc.ShouldEqual(descriptor1.TimestampUtc);
            fetched.Subscriptions.ShouldBeEquivalentTo(descriptor1.Subscriptions);
        }

        [Test]
        public void should_remove_peer()
        {
            var peerDescriptor = _peer1.ToPeerDescriptor(true, typeof(string));

            _repository.AddOrUpdatePeer(peerDescriptor);
            _repository.RemovePeer(peerDescriptor.Peer.Id);

            var fetched = _repository.Get(peerDescriptor.Peer.Id);
            fetched.ShouldBeNull();
        }

        [Test]
        public void should_insert_a_peer_with_no_timestamp_that_was_previously_deleted()
        {
            var descriptor = _peer1.ToPeerDescriptor(true);
            descriptor.TimestampUtc = null;

            _repository.AddOrUpdatePeer(descriptor);

            var fetched = _repository.Get(_peer1.Id);
            fetched.ShouldNotBeNull();
        }

        [Test]
        public void should_add_dynamic_subscriptions()
        {
            var peerDescriptor = _peer1.ToPeerDescriptor(true, typeof(FakeCommand));
            _repository.AddOrUpdatePeer(peerDescriptor);

            _repository.AddDynamicSubscriptionsForTypes(peerDescriptor.PeerId, peerDescriptor.TimestampUtc.Value, new[] { CreateSubscriptionsForType<int>() });

            var fetched = _repository.Get(peerDescriptor.Peer.Id);
            fetched.Subscriptions.ShouldEqual(new[]
            {
                CreateSubscriptionFor<FakeCommand>(),
                CreateSubscriptionFor<int>()
            });
        }


        [Test]
        public void should_get_dynamic_subscriptions_in_GetPeers()
        {
            var peerDescriptor = _peer1.ToPeerDescriptor(true, typeof(FakeCommand));
            _repository.AddOrUpdatePeer(peerDescriptor);

            _repository.AddDynamicSubscriptionsForTypes(peerDescriptor.PeerId, peerDescriptor.TimestampUtc.Value, new[] { CreateSubscriptionsForType<int>() });

            var fetched = _repository.GetPeers().ExpectedSingle();
            fetched.Subscriptions.ShouldEqual(new[]
            {
                CreateSubscriptionFor<FakeCommand>(),
                CreateSubscriptionFor<int>()
            });
        }

        [Test]
        public void should_not_get_dynamic_subscriptions_in_GetPeers_if_called_with_proper_flag()
        {
            var peerDescriptor = _peer1.ToPeerDescriptorWithRoundedTime(true, typeof(FakeCommand));
            _repository.AddOrUpdatePeer(peerDescriptor);

            _repository.AddDynamicSubscriptionsForTypes(peerDescriptor.PeerId, peerDescriptor.TimestampUtc.Value, new[] { CreateSubscriptionsForType<int>() });

            var fetched = _repository.GetPeers(loadDynamicSubscriptions: false).ExpectedSingle();
            fetched.Subscriptions.ShouldEqual(new[]
            {
                CreateSubscriptionFor<FakeCommand>()
            });
        }

        private Subscription CreateSubscriptionFor<TMessage>(params string[] bindingKeyParts)
        {
            return new Subscription(MessageUtil.GetTypeId(typeof(TMessage)), new BindingKey(bindingKeyParts));
        }

        private SubscriptionsForType CreateSubscriptionsForType<TMessage>(params BindingKey[] bindings)
        {
            return new SubscriptionsForType(MessageUtil.GetTypeId(typeof(TMessage)), bindings.Any() ? bindings : new[] { BindingKey.Empty });
        }

        [Test]
        public void should_remove_dynamic_subscriptions()
        {
            var peerDescriptor = _peer1.ToPeerDescriptor(true, typeof(FakeCommand));
            _repository.AddOrUpdatePeer(peerDescriptor);
            _repository.AddDynamicSubscriptionsForTypes(peerDescriptor.PeerId, peerDescriptor.TimestampUtc.Value, new[] { CreateSubscriptionsForType<int>() });

            _repository.RemoveDynamicSubscriptionsForTypes(peerDescriptor.PeerId, peerDescriptor.TimestampUtc.Value, new[] { MessageUtil.GetTypeId(typeof(int)) });

            var fetched = _repository.Get(peerDescriptor.Peer.Id);
            fetched.ShouldHaveSamePropertiesAs(peerDescriptor);
        }

        [Test]
        public void removing_a_dynamic_subscription_doesnt_remove_static_subscription()
        {
            var peerDescriptor = _peer1.ToPeerDescriptor(true, typeof(FakeCommand));
            _repository.AddOrUpdatePeer(peerDescriptor);

            _repository.RemoveDynamicSubscriptionsForTypes(peerDescriptor.PeerId, peerDescriptor.TimestampUtc.Value, new[] { MessageUtil.GetTypeId(typeof(FakeCommand)) });

            var fetched = _repository.Get(peerDescriptor.Peer.Id);
            fetched.ShouldHaveSamePropertiesAs(peerDescriptor);
        }

        [Test]
        public void should_handle_dynamic_subscriptions_with_empty_binding_key_aside_static_one()
        {
            var peerDescriptor = _peer1.ToPeerDescriptor(true, typeof(FakeCommand));
            _repository.AddOrUpdatePeer(peerDescriptor);

            _repository.AddDynamicSubscriptionsForTypes(peerDescriptor.PeerId, peerDescriptor.TimestampUtc.Value, new[] { CreateSubscriptionsForType<FakeCommand>() });

            var fetched = _repository.Get(peerDescriptor.Peer.Id);
            fetched.Subscriptions.ShouldEqual(new[]
            {
                CreateSubscriptionFor<FakeCommand>(),
            });
        }

        [Test]
        public void should_handle_dynamic_subscriptions_with_empty_binding_key_aside_specific_one()
        {
            var peerDescriptor = _peer1.ToPeerDescriptor(true, typeof(FakeCommand));
            _repository.AddOrUpdatePeer(peerDescriptor);

            _repository.AddDynamicSubscriptionsForTypes(peerDescriptor.PeerId, peerDescriptor.TimestampUtc.Value, new[] { CreateSubscriptionsForType<FakeCommand>(BindingKey.Empty, new BindingKey("toto")) });

            var fetched = _repository.Get(peerDescriptor.Peer.Id);
            fetched.Subscriptions.ShouldEqual(new[]
            {
                CreateSubscriptionFor<FakeCommand>(),
                CreateSubscriptionFor<FakeCommand>("toto"),
            });
        }
        
        [Test]
        public void should_deduplicate_dynamic_subscriptions_and_static_subscriptions()
        {
            var peerDescriptor = _peer1.ToPeerDescriptor(true, typeof(FakeCommand));
            _repository.AddOrUpdatePeer(peerDescriptor);

            _repository.AddDynamicSubscriptionsForTypes(peerDescriptor.PeerId, peerDescriptor.TimestampUtc.Value, new[] { CreateSubscriptionsForType<FakeCommand>(), CreateSubscriptionsForType<FakeCommand>() });

            var fetched = _repository.Get(peerDescriptor.Peer.Id);
            fetched.Subscriptions.ShouldEqual(new[] { CreateSubscriptionFor<FakeCommand>() });
        }

        [Test]
        public void should_not_mixup_subscriptions_to_same_type_with_different_tokens()
        {
            var peerDescriptor = _peer1.ToPeerDescriptor(true, typeof(FakeCommand));
            _repository.AddOrUpdatePeer(peerDescriptor);

            _repository.AddDynamicSubscriptionsForTypes(peerDescriptor.PeerId, peerDescriptor.TimestampUtc.Value, new[] { CreateSubscriptionsForType<FakeCommand>(new BindingKey("bli")), CreateSubscriptionsForType<FakeCommand>(new BindingKey("bla")) });

            var fetched = _repository.Get(peerDescriptor.Peer.Id);
            fetched.Subscriptions.ShouldEqual(new[]
            {
                CreateSubscriptionFor<FakeCommand>(),
                CreateSubscriptionFor<FakeCommand>("bli"),
                CreateSubscriptionFor<FakeCommand>("bla")
            });
        }

        [Test]
        public void should_not_add_dynamic_subscriptions_using_outdated_add_command()
        {
            var pastPeerDescriptor = _peer1.ToPeerDescriptor(true, typeof(FakeCommand));
            pastPeerDescriptor.TimestampUtc = SystemDateTime.UtcNow.AddMinutes(-1).RoundToMillisecond();
            var presentPeerDescriptor = _peer1.ToPeerDescriptor(true, typeof(FakeCommand));
            _repository.AddOrUpdatePeer(presentPeerDescriptor);

            _repository.RemoveDynamicSubscriptionsForTypes(presentPeerDescriptor.PeerId, presentPeerDescriptor.TimestampUtc.Value, new[] { MessageUtil.GetTypeId(typeof(int)) });
            _repository.AddDynamicSubscriptionsForTypes(pastPeerDescriptor.PeerId, pastPeerDescriptor.TimestampUtc.Value, new[] { CreateSubscriptionsForType<int>() });

            var fetched = _repository.Get(presentPeerDescriptor.Peer.Id);
            fetched.Subscriptions.ShouldEqual(new[] { CreateSubscriptionFor<FakeCommand>() });
        }

        [Test]
        public void should_not_remove_dynamic_subscriptions_using_outdated_delete_command()
        {
            var pastPeerDescriptor = _peer1.ToPeerDescriptor(true, typeof(FakeCommand));
            pastPeerDescriptor.TimestampUtc = SystemDateTime.UtcNow.AddMinutes(-1).RoundToMillisecond();
            var presentPeerDescriptor = _peer1.ToPeerDescriptor(true, typeof(FakeCommand));
            _repository.AddOrUpdatePeer(presentPeerDescriptor);
            _repository.AddDynamicSubscriptionsForTypes(presentPeerDescriptor.PeerId, presentPeerDescriptor.TimestampUtc.Value, new[] { CreateSubscriptionsForType<int>() });

            _repository.AddDynamicSubscriptionsForTypes(presentPeerDescriptor.PeerId, presentPeerDescriptor.TimestampUtc.Value, new[] { CreateSubscriptionsForType<int>() });
            _repository.RemoveDynamicSubscriptionsForTypes(pastPeerDescriptor.PeerId, pastPeerDescriptor.TimestampUtc.Value, new[] { MessageUtil.GetTypeId(typeof(int)) });


            var fetched = _repository.Get(presentPeerDescriptor.Peer.Id);
            fetched.Subscriptions.ShouldEqual(new[]
            {
                CreateSubscriptionFor<FakeCommand>(),
                CreateSubscriptionFor<int>()
            });
        }

        [Test]
        public void should_remove_dynamic_subscriptions_for_peer()
        {
            var firstPeer = _peer1.ToPeerDescriptor(true, typeof(FakeCommand));
            var secondPeer = _peer2.ToPeerDescriptor(true, typeof(FakeCommand));
            _repository.AddOrUpdatePeer(firstPeer);
            _repository.AddOrUpdatePeer(secondPeer);
            _repository.AddDynamicSubscriptionsForTypes(firstPeer.PeerId, DateTime.UtcNow, new[] { CreateSubscriptionsForType<FakeCommand>(new BindingKey("toto")) });
            _repository.AddDynamicSubscriptionsForTypes(secondPeer.PeerId, DateTime.UtcNow, new[] { CreateSubscriptionsForType<FakeCommand>(new BindingKey("toto")) });

            _repository.RemoveAllDynamicSubscriptionsForPeer(secondPeer.PeerId, secondPeer.TimestampUtc.Value);

            var fetched = _repository.GetPeers().ToList();
            fetched.Count.ShouldEqual(2);
            var untouchedPeer = fetched.Single(peer => peer.PeerId == firstPeer.PeerId);
            var peerWithTruncatedSubscriptions = fetched.Single(peer => peer.PeerId == secondPeer.PeerId);
            untouchedPeer.Subscriptions.ShouldEqual(new[]
            {
                CreateSubscriptionFor<FakeCommand>(),
                CreateSubscriptionFor<FakeCommand>("toto")
            });
            peerWithTruncatedSubscriptions.Subscriptions.ShouldEqual(new[]
            {
                CreateSubscriptionFor<FakeCommand>()
            });
        }
    }
}
