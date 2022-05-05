using System;
using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Directory;
using Abc.Zebus.Routing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Messages;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Routing
{
    /// <summary>
    /// Ensure alternative routing matching methods have consistent results.
    /// </summary>
    [TestFixture]
    public class RoutingMatchingTests
    {
        [TestCase("#", true)]
        [TestCase("*.*.*", true)]
        [TestCase("A.*.*", true)]
        [TestCase("9.*.*", false)]
        [TestCase("*.1.*", true)]
        [TestCase("*.9.*", false)]
        [TestCase("*.*.101", true)]
        [TestCase("*.*.999", false)]
        [TestCase("A.3.102", true)]
        [TestCase("A.3.999", false)]
        [TestCase("A.9.102", false)]
        [TestCase("9.3.102", false)]
        public void should_match_routable_message(string bindingKeyText, bool isMatchExpected)
        {
            var message = new FakeRoutableCommandWithCollection
            {
                Name = "A",
                IdArray = new[] { 1, 2, 3 },
                ValueList = new List<decimal> { 101m, 102m },
            };

            var bindingKey = BindingKeyHelper.CreateFromString(bindingKeyText, '.');

            ValidateMatching(message, bindingKey, isMatchExpected);
        }

        private static void ValidateMatching(IMessage message, BindingKey bindingKey, bool isMatchExpected)
        {
            var messageTypeId = message.TypeId();
            var messageBinding = MessageBinding.FromMessage(message);

            var subscription = new Subscription(messageTypeId, bindingKey);
            subscription.Matches(messageBinding).ShouldEqual(isMatchExpected, "Subscription should match");

            var predicate = BindingKeyUtil.BuildPredicate(messageTypeId, bindingKey);
            predicate.Invoke(message).ShouldEqual(isMatchExpected, "Predicate should match");

            var subscriptionTree = new PeerSubscriptionTree();
            subscriptionTree.Add(TestDataBuilder.Peer(), bindingKey);

            var peers = subscriptionTree.GetPeers(messageBinding.RoutingContent);
            peers.Any().ShouldEqual(isMatchExpected, "PeerSubscriptionTree should match");
        }
    }
}
