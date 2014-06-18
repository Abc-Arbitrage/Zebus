using System;
using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Directory;
using Abc.Zebus.Routing;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Measurements;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Directory
{
    public partial class PeerDirectoryClientTests
    {
        [Test, Ignore("Performance test")]
        public void MeasureUpdatePerformance()
        {
            var subscriptions = new List<Subscription>();
            for (var typeIdIndex = 0; typeIdIndex < 20; ++typeIdIndex)
            {
                var typeId = new MessageTypeId("Abc.Foo.Events.FakeEvent" + typeIdIndex);
                for (var routingIndex = 0; routingIndex < 500; ++routingIndex)
                {
                    subscriptions.Add(new Subscription(typeId, new BindingKey(routingIndex.ToString())));
                }
            }

            _directory = new PeerDirectoryClient(_configurationMock.Object);
            _directory.Handle(new PeerStarted(_otherPeer.ToPeerDescriptor(false)));

            Console.WriteLine("Snapshot updates (add)");
            using (Measure.Throughput(subscriptions.Count))
            {
                for (var subscriptionCount = 1; subscriptionCount <= subscriptions.Count; ++subscriptionCount)
                {
                    _directory.Handle(new PeerSubscriptionsUpdated(_otherPeer.ToPeerDescriptor(false, subscriptions.Take(subscriptionCount))));
                }
            }
            Console.WriteLine("Snapshot updates (remove)");
            using (Measure.Throughput(subscriptions.Count))
            {
                for (var subscriptionCount = subscriptions.Count; subscriptionCount >= 1; --subscriptionCount)
                {
                    _directory.Handle(new PeerSubscriptionsUpdated(_otherPeer.ToPeerDescriptor(false, subscriptions.Take(subscriptionCount))));
                }
            }

            _directory = new PeerDirectoryClient(_configurationMock.Object);
            _directory.Handle(new PeerStarted(_otherPeer.ToPeerDescriptor(false)));

            // TODO CAO

            //Console.WriteLine("Incremental updates (add)");
            //using (Measure.Throughput(subscriptions.Count))
            //{
            //    for (var subscriptionIndex = 0; subscriptionIndex <subscriptions.Count; ++subscriptionIndex)
            //    {
            //        _directory.Handle(new PeerSubscriptionsAdded(_otherPeer.Id, new[] { subscriptions[subscriptionIndex] }, DateTime.UtcNow));
            //    }
            //}
            //Console.WriteLine("Incremental updates (remove)");
            //using (Measure.Throughput(subscriptions.Count))
            //{
            //    for (var subscriptionIndex = subscriptions.Count - 1; subscriptionIndex >= 0; --subscriptionIndex)
            //    //for (var subscriptionIndex = 0; subscriptionIndex < subscriptions.Count; subscriptionIndex += 200)
            //    {
            //        _directory.Handle(new PeerSubscriptionsRemoved(_otherPeer.Id, new[] { subscriptions[subscriptionIndex] }, DateTime.UtcNow));
            //        //_directory.Handle(new PeerSubscriptionsRemoved(_otherPeer.Id, subscriptions.GetRange(subscriptionIndex, 200).ToArray(), DateTime.UtcNow));
            //    }
            //}
        }
    }
}