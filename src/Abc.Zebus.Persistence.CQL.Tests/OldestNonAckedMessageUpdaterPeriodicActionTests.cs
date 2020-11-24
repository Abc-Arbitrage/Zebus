using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Persistence.CQL.Data;
using Abc.Zebus.Persistence.CQL.PeriodicAction;
using Abc.Zebus.Persistence.CQL.Storage;
using Abc.Zebus.Persistence.CQL.Tests.Cql;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Persistence.CQL.Tests
{
    public class OldestNonAckedMessageUpdaterPeriodicActionTests : CqlTestFixture<PersistenceCqlDataContext, ICqlPersistenceConfiguration>
    {
        private TestBus _testBus;
        private OldestNonAckedMessageUpdaterPeriodicAction _oldestMessageUpdater;
        private Mock<ICqlStorage> _cqlStorage;

        public override void CreateSchema()
        {
            IgnoreWhenSet("GITHUB_ACTIONS");
            base.CreateSchema();
        }

        private static void IgnoreWhenSet(string environmentVariable)
        {
            var env = Environment.GetEnvironmentVariable(environmentVariable);
            if (!string.IsNullOrEmpty(env) && bool.TryParse(env, out var isSet) && isSet)
                Assert.Ignore("We need a cassandra node for this");
        }

        [SetUp]
        public void SetUp()
        {
            _testBus = new TestBus();
            var configurationMock = new Mock<ICqlPersistenceConfiguration>();
            configurationMock.SetupGet(x => x.OldestMessagePerPeerCheckPeriod).Returns(30.Seconds());
            configurationMock.SetupGet(x => x.OldestMessagePerPeerGlobalCheckPeriod).Returns(30.Seconds());
            _cqlStorage = new Mock<ICqlStorage>();
            _oldestMessageUpdater = new OldestNonAckedMessageUpdaterPeriodicAction(_testBus, configurationMock.Object, _cqlStorage.Object);
        }

        [Test]
        public void should_call_clean_up_buckets_global_check()
        {
            var peerState = new PeerState(new PeerId("Peer"));
            var otherPeerState = new PeerState(new PeerId("OtherPeer"));
            _cqlStorage.Setup(s => s.GetAllKnownPeers()).Returns(new[] { peerState, otherPeerState, });

            _oldestMessageUpdater.DoPeriodicAction();

            _cqlStorage.Verify(x => x.UpdateNewOldestMessageTimestamp(peerState), Times.Once);
            _cqlStorage.Verify(x => x.UpdateNewOldestMessageTimestamp(otherPeerState), Times.Once);
        }

        [Test]
        public void should_call_clean_up_buckets()
        {
            _oldestMessageUpdater.DoPeriodicAction();

            var peerState = new PeerState(new PeerId("Peer"));
            var otherPeerState = new PeerState(new PeerId("OtherPeer"));
            _cqlStorage.Setup(s => s.GetAllKnownPeers()).Returns(new[] { peerState, otherPeerState, });

            _oldestMessageUpdater.DoPeriodicAction();

            _cqlStorage.Verify(x => x.UpdateNewOldestMessageTimestamp(peerState), Times.Once);
            _cqlStorage.Verify(x => x.UpdateNewOldestMessageTimestamp(otherPeerState), Times.Once);
        }

        [Test]
        public void should_only_call_clean_for_updated_peers()
        {
            var peerStates = new[]
            {
                new PeerState(new PeerId("Peer")),
                new PeerState(new PeerId("OtherPeer"))
            };

            var cleanedPeerStates = new List<PeerState>();

            _cqlStorage.Setup(s => s.GetAllKnownPeers()).Returns(peerStates);
            _cqlStorage.Setup(s => s.UpdateNewOldestMessageTimestamp(Capture.In(cleanedPeerStates))).Returns(Task.CompletedTask);

            _oldestMessageUpdater.DoPeriodicAction();

            peerStates[0] = peerStates[0].WithNonAckedMessageCountDelta(1);

            _oldestMessageUpdater.DoPeriodicAction();

            cleanedPeerStates.Select(x => x.PeerId)
                             .ShouldBeEquivalentTo(peerStates[0].PeerId, peerStates[1].PeerId, peerStates[0].PeerId);
        }
    }
}
