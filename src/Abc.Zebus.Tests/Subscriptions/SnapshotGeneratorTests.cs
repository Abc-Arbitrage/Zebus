using Abc.Zebus.Directory;
using Abc.Zebus.Subscriptions;
using Abc.Zebus.Testing;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Subscriptions
{
    [TestFixture]
    public class SnapshotGeneratorTests
    {
        private class TestEvent : IEvent
        {
        }

        private class TestSnapshot : IEvent
        {
        }

        private class TestSnapshotGenerator : SubscriptionSnapshotGenerator<TestSnapshot, TestEvent>
        {
            private readonly TestSnapshot _testSnapshot;

            public TestSnapshotGenerator(IBus bus, TestSnapshot testSnapshot)
                : base(bus)
            {
                _testSnapshot = testSnapshot;
            }

            protected override TestSnapshot GenerateSnapshot(SubscriptionsForType messageSubscription)
            {
                return _testSnapshot;
            }
        }

        [Test]
        public void should_generate_snapshot_and_publish_it_to_the_specified_peer()
        {
            // Arrange
            var testBus = new TestBus();
            var snapshotGenerator = new TestSnapshotGenerator(testBus, new TestSnapshot());

            // Act
            var peerId = new PeerId("testPeerId");
            snapshotGenerator.Handle(new SubscriptionsUpdated(new SubscriptionsForType(new MessageTypeId(typeof(TestEvent))), peerId));

            // Assert
            testBus.ExpectExactly(peerId, new TestSnapshot());
        }

        [Test]
        public void should_not_generate_snapshot_if_snapshot_generator_returns_null()
        {
            // Arrange
            var testBus = new TestBus();
            var snapshotGenerator = new TestSnapshotGenerator(testBus, null);

            // Act
            var peerId = new PeerId("testPeerId");
            snapshotGenerator.Handle(new SubscriptionsUpdated(new SubscriptionsForType(new MessageTypeId(typeof(TestEvent))), peerId));

            // Assert
            testBus.ExpectNothing();
        }
    }
}
