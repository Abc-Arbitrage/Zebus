using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Directory.Configuration;
using Abc.Zebus.Directory.DeadPeerDetection;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Directory.Tests.DeadPeerDetection
{
    [TestFixture]
    public class DeadPeerDetectorEntryTests
    {
        private DeadPeerDetectorEntry _entry;
        private Mock<IDirectoryConfiguration> _configurationMock;
        private TestBus _bus;

        [SetUp]
        public void Setup()
        {
            _configurationMock = new Mock<IDirectoryConfiguration>();
            _bus = new TestBus();

            var peer = new Peer(new PeerId("Abc.Testing.0"), "tcp://abctest:12300");
            var peerDescriptor = peer.ToPeerDescriptor(true);
            _entry = new DeadPeerDetectorEntry(peerDescriptor, _configurationMock.Object, _bus, new CurrentThreadTaskScheduler());
        }

        [Test]
        public void should_use_debug_timeout()
        {
            _configurationMock.SetupGet(x => x.PersistentPeerPingTimeout).Returns(5.Seconds());
            _configurationMock.SetupGet(x => x.DebugPeerPingTimeout).Returns(500.Seconds());

            _bus.HandlerExecutor = new TestBus.DoNotReplyHandlerExecutor();
            _entry.Descriptor.HasDebuggerAttached = true;

            var pingTimestamp = SystemDateTime.UtcNow;
            _entry.Ping(pingTimestamp);

            using (SystemDateTime.Set(pingTimestamp.AddSeconds(15)))
            {
                _entry.HasReachedTimeout().ShouldBeFalse();
            }
            using (SystemDateTime.Set(pingTimestamp.AddSeconds(501)))
            {
                _entry.HasReachedTimeout().ShouldBeTrue();
            }
        }

        [Test]
        public void should_use_persistent_timeout()
        {
            _configurationMock.SetupGet(x => x.TransientPeerPingTimeout).Returns(5.Seconds());
            _configurationMock.SetupGet(x => x.PersistentPeerPingTimeout).Returns(500.Seconds());

            _bus.HandlerExecutor = new TestBus.DoNotReplyHandlerExecutor();
            _entry.Descriptor.IsPersistent = true;

            var pingTimestamp = SystemDateTime.UtcNow;
            _entry.Ping(pingTimestamp);

            using (SystemDateTime.Set(pingTimestamp.AddSeconds(15)))
            {
                _entry.HasReachedTimeout().ShouldBeFalse();
            }
            using (SystemDateTime.Set(pingTimestamp.AddSeconds(501)))
            {
                _entry.HasReachedTimeout().ShouldBeTrue();
            }
        }

        [Test]
        public void should_use_transcient_timeout()
        {
            _configurationMock.SetupGet(x => x.TransientPeerPingTimeout).Returns(5.Seconds());

            _bus.HandlerExecutor = new TestBus.DoNotReplyHandlerExecutor();
            _entry.Descriptor.IsPersistent = false;

            var pingTimestamp = SystemDateTime.UtcNow;
            _entry.Ping(pingTimestamp);

            using (SystemDateTime.Set(pingTimestamp.AddSeconds(4)))
            {
                _entry.HasReachedTimeout().ShouldBeFalse();
            }
            using (SystemDateTime.Set(pingTimestamp.AddSeconds(6)))
            {
                _entry.HasReachedTimeout().ShouldBeTrue();
            }
        }

        [Test]
        public void should_not_detect_responding_peer_twice()
        {
            var pingTimestampUtc = SystemDateTime.UtcNow;

            _entry.Descriptor.Peer.IsResponding = false;

            var manualResetEvent = new ManualResetEventSlim();
            var peerRespondingCount = 0;
            _entry.PeerRespondingDetected += (e, o) =>
            {
                manualResetEvent.Wait();
                peerRespondingCount++;
            };

            var ackTask1 = Task.Run(() => _entry.OnPingCommandAck(Task.FromResult(new CommandResult(0, null)), pingTimestampUtc));
            var ackTask2 = Task.Run(() => _entry.OnPingCommandAck(Task.FromResult(new CommandResult(0, null)), pingTimestampUtc));

            Thread.Sleep(10);
            manualResetEvent.Set();

            Task.WaitAll(ackTask1, ackTask2);

            peerRespondingCount.ShouldEqual(1);
        }
    }
}