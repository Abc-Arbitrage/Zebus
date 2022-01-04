using System;
using Abc.Zebus.Directory.Cassandra.Data;
using Abc.Zebus.Testing.Extensions;
using NUnit.Framework;

namespace Abc.Zebus.Directory.Cassandra.Tests.Data
{
    public class CassandraExtensionsTests
    {
        [Test]
        public void should_return_a_storage_peer_with_its_timestamp_kind_set_to_utc()
        {
            var unspecifiedKindUtcNow = new DateTime(DateTime.UtcNow.Ticks, DateTimeKind.Unspecified);
            var peerDescriptor = new PeerDescriptor(new PeerId("Abc.Titi.0"), "tcp://toto:123", false, true, true, unspecifiedKindUtcNow);

            var peer = peerDescriptor.ToCassandra();
            peer.TimestampUtc.Kind.ShouldEqual(DateTimeKind.Utc);
        }

        [Test]
        public void should_return_a_peer_descriptor_with_its_timestamp_kind_set_to_utc()
        {
            var unspecifiedKindUtcNow = new DateTime(DateTime.UtcNow.Ticks, DateTimeKind.Unspecified);
            var peer = new CassandraPeer { TimestampUtc = unspecifiedKindUtcNow, StaticSubscriptionsBytes = new byte[0]};

            var peerDescriptor = peer.ToPeerDescriptor(new Subscription[0]);
            peerDescriptor.TimestampUtc.Value.Kind.ShouldEqual(DateTimeKind.Utc);
        }
    }
}
