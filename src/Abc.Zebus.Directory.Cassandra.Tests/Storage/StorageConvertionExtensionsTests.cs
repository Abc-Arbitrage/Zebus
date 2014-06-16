using System;
using Abc.Zebus.Directory.Cassandra.Storage;
using Abc.Zebus.Testing.Extensions;
using NUnit.Framework;

namespace Abc.Zebus.Directory.Cassandra.Tests.Storage
{
    public class StorageConvertionExtensionsTests
    {
        [Test]
        public void should_return_a_storage_peer_with_its_timestamp_kind_set_to_utc()
        {
            var unspecifiedKindUtcNow = new DateTime(DateTime.UtcNow.Ticks, DateTimeKind.Unspecified);
            var peerDescriptor = new PeerDescriptor(new PeerId("Abc.Titi.0"), "tcp://toto:123", false, true, true, unspecifiedKindUtcNow);

            var storagePeer = peerDescriptor.ToStoragePeer();

            storagePeer.TimestampUtc.Kind.ShouldEqual(DateTimeKind.Utc);
        }

        [Test]
        public void should_return_a_peer_descriptor_with_its_timestamp_kind_set_to_utc()
        {
            var unspecifiedKindUtcNow = new DateTime(DateTime.UtcNow.Ticks, DateTimeKind.Unspecified);
            var storagePeer = new StoragePeer { TimestampUtc = unspecifiedKindUtcNow, StaticSubscriptionsBytes = new byte[0]};

            var peerDescriptor = storagePeer.ToPeerDescriptor(new Subscription[0]);

            peerDescriptor.TimestampUtc.Value.Kind.ShouldEqual(DateTimeKind.Utc);
        }
    }
}