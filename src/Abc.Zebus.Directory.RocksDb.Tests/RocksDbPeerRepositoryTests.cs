using NUnit.Framework;
using Abc.Zebus.Directory.RocksDb.Storage;
using Abc.Zebus.Testing.Extensions;
using System;

namespace Abc.Zebus.Directory.RocksDb.Tests
{
    [TestFixture]
    public class RocksDbPeerRepositoryTests
    {
        private RocksDbPeerRepository _repository;

        [SetUp]
        public void Setup()
        {
            _repository = new RocksDbPeerRepository();
            _repository.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _repository.Dispose();
        }

        [Test]
        public void should_add_and_get_peer_descriptor()
        {
            var peerId = new PeerId("lol");
            var sourcePeerDescriptor = new PeerDescriptor(peerId, "endpoint", true, true, true, DateTime.UtcNow, new[] { Subscription.Any<TestMessage>() });
            _repository.AddOrUpdatePeer(sourcePeerDescriptor);
            var peerDescriptor = _repository.Get(peerId);

            peerDescriptor.ShouldNotBeNull();
            peerDescriptor.ShouldEqualDeeply(sourcePeerDescriptor);
        }

        class TestMessage : IMessage { }
    }
}
