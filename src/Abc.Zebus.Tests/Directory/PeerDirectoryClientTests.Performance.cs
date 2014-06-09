using System.Collections.Generic;
using Abc.Zebus.Directory;
using Abc.Zebus.Routing;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Measurements;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Directory
{
    public partial class PeerDirectoryClientTests
    {
        [Test, Ignore("Performance tests")]
        public void MeasureUpdatePerformance()
        {
            _directory.Handle(new PeerStarted(_otherPeer.ToPeerDescriptor(false)));

            var subscriptions = new List<Subscription>();

            using (Measure.Throughput(10000))
            {
                for (var typeIdIndex = 0; typeIdIndex < 20; ++typeIdIndex)
                {
                    var typeId = new MessageTypeId("Abc.Oms.Events.PositionPoleTnaId" + typeIdIndex);
                    for (var routingIndex = 0; routingIndex < 500; ++routingIndex)
                    {
                        subscriptions.Add(new Subscription(typeId, new BindingKey(routingIndex.ToString())));

                        _directory.Handle(new PeerSubscriptionsUpdated(_otherPeer.ToPeerDescriptor(false, subscriptions)));
                    }
                }
            }

            Measure.Execution(100, () => _directory.Handle(new PeerSubscriptionsUpdated(_otherPeer.ToPeerDescriptor(false, subscriptions))));

            // Elapsed(ms):    23409,46
            // Elapsed(ms):    20357,56
        }
    }
}