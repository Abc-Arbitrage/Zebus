using System.Collections.Generic;
using Abc.Zebus.Directory;
using Abc.Zebus.SubscriptionHandling;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using NUnit.Framework;

namespace Abc.Zebus.Tests.SubscriptionHandling
{

    [TestFixture]
    public class SnapshotGeneratorTests
    {
        private class EventTest : IEvent
        {

        }
        private class SnapshotTest : IEvent
        {
        }

        private class snapshotGeneratorTest : SubscriptionSnapshotGenerator<SnapshotTest, EventTest>
        {
            public snapshotGeneratorTest(IBus bus)
                : base(bus)
            {
            }

            protected override SnapshotTest GenerateSnapshot(SubscriptionsForType messageSubscription)
            {
                return new SnapshotTest();
            }
        }

        [Test]
        public void should_Generate_snapshot_and_publish_it_to_the_specified_peer()
        {
            // Arrange
            var testBus = new TestBus();
            var snapshotGenerator = new snapshotGeneratorTest(testBus);

            // Act
            var peerId = new PeerId("testPeerId");
            snapshotGenerator.Handle(new SubscriptionUpdatedMessage(new SubscriptionsForType(new MessageTypeId(typeof(EventTest))), peerId));

            // Assert
            testBus.ExpectExactly(peerId, new SnapshotTest());

        }


    }
}
