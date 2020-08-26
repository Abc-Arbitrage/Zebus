using System.Collections.Generic;
using Abc.Zebus.Directory;
using Abc.Zebus.Subscriptions;
using Abc.Zebus.Testing;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Subscriptions
{
    [TestFixture]
    public class SubscriptionMultipleSnapshotGeneratorTests
    {
        private class TestEvent : IEvent
        {
        }

        private class TestSnapshot : IEvent
        {
        }

        private class TestSubscriptionMultipleSnapshotGenerator : SubscriptionMultipleSnapshotGenerator<TestSnapshot, TestEvent>
        {
            private readonly IEnumerable<TestSnapshot> _messages;

            public TestSubscriptionMultipleSnapshotGenerator(IBus bus, IEnumerable<TestSnapshot> messages)
                : base(bus)
                => _messages = messages;

            protected override IEnumerable<TestSnapshot> GenerateSnapshots(SubscriptionsForType messageSubscription) => _messages;
        }

        [Test]
        public void should_generate_snapshot_and_publish_it_to_the_specified_peer()
        {
            // Arrange
            var testBus = new TestBus();
            var snapshotGenerator = new TestSubscriptionMultipleSnapshotGenerator(testBus, new []{new TestSnapshot(), new TestSnapshot(), });

            // Act
            var peerId = new PeerId("testPeerId");
            snapshotGenerator.Handle(new SubscriptionsUpdated(new SubscriptionsForType(new MessageTypeId(typeof(TestEvent))), peerId));

            // Assert
            testBus.ExpectExactly(peerId, new TestSnapshot(), new TestSnapshot());
        }

        [Test]
        public void should_generate_empty_snapshot_and_publish_it_to_the_specified_peer()
        {
            // Arrange
            var testBus = new TestBus();
            var snapshotGenerator = new TestSubscriptionMultipleSnapshotGenerator(testBus, new TestSnapshot[0]);

            // Act
            var peerId = new PeerId("testPeerId");
            snapshotGenerator.Handle(new SubscriptionsUpdated(new SubscriptionsForType(new MessageTypeId(typeof(TestEvent))), peerId));

            // Assert
            testBus.ExpectNothing();
        }
    }
}
