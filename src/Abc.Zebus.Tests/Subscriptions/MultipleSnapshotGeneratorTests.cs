using System.Collections.Generic;
using Abc.Zebus.Directory;
using Abc.Zebus.Subscriptions;
using Abc.Zebus.Testing;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Subscriptions
{
    [TestFixture]
    public class MultipleSnapshotGeneratorTests
    {
        private class EventTest : IEvent
        {
        }

        private class SnapshotTest : IEvent
        {
        }

        private class MultipleSnapshotGeneratorTest : MultipleSubscriptionSnapshotGenerator<SnapshotTest, EventTest>
        {
            private readonly IEnumerable<SnapshotTest> _messages;

            public MultipleSnapshotGeneratorTest(IBus bus, IEnumerable<SnapshotTest> messages)
                : base(bus)
                => _messages = messages;

            protected override IEnumerable<SnapshotTest> GenerateSnapshots(SubscriptionsForType messageSubscription) => _messages;
        }

        [Test]
        public void should_generate_snapshot_and_publish_it_to_the_specified_peer()
        {
            // Arrange
            var testBus = new TestBus();
            var snapshotGenerator = new MultipleSnapshotGeneratorTest(testBus, new []{new SnapshotTest(), new SnapshotTest(), });

            // Act
            var peerId = new PeerId("testPeerId");
            snapshotGenerator.Handle(new SubscriptionsUpdated(new SubscriptionsForType(new MessageTypeId(typeof(EventTest))), peerId));

            // Assert
            testBus.ExpectExactly(peerId, new SnapshotTest(), new SnapshotTest());
        }

        [Test]
        public void should_generate_empty_snapshot_and_publish_it_to_the_specified_peer()
        {
            // Arrange
            var testBus = new TestBus();
            var snapshotGenerator = new MultipleSnapshotGeneratorTest(testBus, new SnapshotTest[0]);

            // Act
            var peerId = new PeerId("testPeerId");
            snapshotGenerator.Handle(new SubscriptionsUpdated(new SubscriptionsForType(new MessageTypeId(typeof(EventTest))), peerId));

            // Assert
            testBus.ExpectNothing();
        }
    }
}
