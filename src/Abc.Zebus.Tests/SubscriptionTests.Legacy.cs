using Abc.Zebus.Routing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Messages;
using NUnit.Framework;

namespace Abc.Zebus.Tests
{
    public partial class SubscriptionTests
    {
        [Test]
        public void should_match_joined_routing_key_with_single_token_subscription()
        {
            var subscription = new Subscription(MessageUtil.TypeId<FakeRoutableCommand>(), new BindingKey("Abc.Service.0"));

            var routingKey = BindingKey.Joined("Abc.Service.0");
            subscription.Matches(routingKey).ShouldBeTrue();
        }

        [Test]
        public void should_not_match_invalid_joined_routing_key_with_single_token_subscription()
        {
            var subscription = new Subscription(MessageUtil.TypeId<FakeRoutableCommand>(), new BindingKey("Abc.Service.0"));

            var routingKey = BindingKey.Joined("Abc.Service.1");
            subscription.Matches(routingKey).ShouldBeFalse();
        }

        [Test]
        public void should_match_joined_routing_key_with_splitted_token_subscription()
        {
            var subscription = new Subscription(MessageUtil.TypeId<FakeRoutableCommand>(), new BindingKey("Abc", "Service", "0"));

            var routingKey = BindingKey.Joined("Abc.Service.0");
            subscription.Matches(routingKey).ShouldBeTrue();
        }

        [Test]
        public void should_match_joined_routing_key_with_splitted_token_subscription_and_wildcard_1()
        {
            var subscription = new Subscription(MessageUtil.TypeId<FakeRoutableCommand>(), new BindingKey("Abc", "Service", "*", "Foo"));

            var routingKey = BindingKey.Joined("Abc.Service.42.Foo");
            subscription.Matches(routingKey).ShouldBeTrue();
        }

        [Test]
        public void should_match_joined_routing_key_with_splitted_token_subscription_and_wildcard_2()
        {
            var subscription = new Subscription(MessageUtil.TypeId<FakeRoutableCommand>(), new BindingKey("Abc", "#"));

            var routingKey = BindingKey.Joined("Abc.Service.42");
            subscription.Matches(routingKey).ShouldBeTrue();
        }

        [Test]
        public void should_not_match_invalid_joined_routing_key_with_splitted_token_subscription()
        {
            var subscription = new Subscription(MessageUtil.TypeId<FakeRoutableCommand>(), new BindingKey("Abc", "Service", "0"));

            var routingKey = BindingKey.Joined("Abc.Service.1");
            subscription.Matches(routingKey).ShouldBeFalse();
        }

        [Test]
        public void should_match_joined_qpid_message_with_machine_name()
        {
            var bindingKey = BindingKey.Joined("machinename.Abc.Foo.0");
            var subscription = Subscription.Matching<InstanceHeartBeat>(x => x.InstanceName == "Abc.Foo.0");

            subscription.Matches(bindingKey).ShouldBeTrue();
        }

        [Test]
        public void should_match_joined_qpid_message_without_machine_name()
        {
            var bindingKey = BindingKey.Joined(".Abc.Foo.0");
            var subscription = Subscription.Matching<InstanceHeartBeat>(x => x.InstanceName == "Abc.Foo.0");

            subscription.Matches(bindingKey).ShouldBeTrue();
        }

        [Routable]
        public class InstanceHeartBeat : IEvent
        {
            [RoutingPosition(2)]
            public string InstanceName;

            [RoutingPosition(1)]
            public string MachineName;
        }
    }
}