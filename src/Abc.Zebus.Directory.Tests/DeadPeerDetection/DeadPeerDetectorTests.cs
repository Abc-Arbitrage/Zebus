using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Directory.Configuration;
using Abc.Zebus.Directory.DeadPeerDetection;
using Abc.Zebus.Directory.Storage;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Directory.Tests.DeadPeerDetection
{
    [TestFixture]
    public class DeadPeerDetectorTests
    {
        private const int _transientPeerTimeout = 60 * 10;
        private const int _persistentPeerTimeout = 60 * 30;
        private const int _debugPeerTimeout = 60 * 60;

        private readonly TimeSpan _pingInterval = 5.Minutes();
        private PeerDescriptor _transientAlivePeer0;
        private PeerDescriptor _transientAlivePeer1;
        private PeerDescriptor _transientDeadPeer;
        private PeerDescriptor _persistentAlivePeer;
        private PeerDescriptor _persistentDeadPeer;
        private PeerDescriptor _debugPersistentAlivePeer;
        private PeerDescriptor _debugTransientAlivePeer;
        private Mock<IPeerRepository> _peerRepositoryMock;
        private TestBus _bus;
        private DeadPeerDetector _detector;
        private Mock<IDirectoryConfiguration> _configurationMock;
        private PeerDescriptor _persistencePeer;

        [SetUp]
        public void Setup()
        {
            _transientAlivePeer0 = CreatePeerDescriptor("Abc.TransientAlive.0", isUp: true, isPersistent: false, hasDebuggerAttached:false);
            _transientAlivePeer1 = CreatePeerDescriptor("Abc.TransientAlive.1", isUp: true, isPersistent: false, hasDebuggerAttached: false);
            _transientDeadPeer = CreatePeerDescriptor("Abc.TransientDead.0", isUp: false, isPersistent: false, hasDebuggerAttached: false);
            _persistentAlivePeer = CreatePeerDescriptor("Abc.PersistentAlive.0", isUp: true, isPersistent: true, hasDebuggerAttached: false);
            _persistentDeadPeer = CreatePeerDescriptor("Abc.PersistentDead.0", isUp: false, isPersistent: true, hasDebuggerAttached: false);
            _debugPersistentAlivePeer = CreatePeerDescriptor("Abc.DebugPersistentAlive.0", isUp: true, isPersistent: true, hasDebuggerAttached: true);
            _debugTransientAlivePeer = CreatePeerDescriptor("Abc.DebugTransientAlive.0", isUp: true, isPersistent: false, hasDebuggerAttached: true);
            _persistencePeer = CreatePeerDescriptor("Abc.Zebus.PersistenceService.0", isUp: true, isPersistent: false, hasDebuggerAttached: false);

            _peerRepositoryMock = new Mock<IPeerRepository>();
            var peerDescriptors = new List<PeerDescriptor>
            {
                _transientAlivePeer0,
                _transientAlivePeer1,
                _transientDeadPeer,
                _persistentAlivePeer,
                _persistentDeadPeer,
                _debugPersistentAlivePeer,
                _debugTransientAlivePeer,
                _persistencePeer
            };
            _peerRepositoryMock.Setup(repo => repo.GetPeers()).Returns(peerDescriptors);
            _peerRepositoryMock.Setup(repo => repo.Get(It.IsAny<PeerId>())).Returns<PeerId>(peerId => peerDescriptors.FirstOrDefault(x => x.Peer.Id == peerId));

            _bus = new TestBus();

            _configurationMock = new Mock<IDirectoryConfiguration>();
            _configurationMock.As<IDirectoryConfiguration>().SetupGet(conf => conf.TransientPeerPingTimeout).Returns(_transientPeerTimeout.Seconds());
            _configurationMock.As<IDirectoryConfiguration>().SetupGet(conf => conf.PersistentPeerPingTimeout).Returns(_persistentPeerTimeout.Seconds());
            _configurationMock.As<IDirectoryConfiguration>().SetupGet(conf => conf.DebugPeerPingTimeout).Returns(_debugPeerTimeout.Seconds());
            _configurationMock.As<IDirectoryConfiguration>().SetupGet(conf => conf.PeerPingInterval).Returns(_pingInterval);

            _detector = new DeadPeerDetector(_bus, _peerRepositoryMock.Object, _configurationMock.Object);
            _detector.TaskScheduler = new CurrentThreadTaskScheduler();

            _bus.HandlerExecutor = new HangOnThrowHandlerExecutor();
        }

        [Test]
        public void should_ping_peers()
        {
            _detector.DetectDeadPeers();

            _bus.ExpectExactly(_transientAlivePeer0.PeerId, new PingPeerCommand());
            _bus.ExpectExactly(_transientAlivePeer1.PeerId, new PingPeerCommand());
            _bus.ExpectExactly(_persistentAlivePeer.PeerId, new PingPeerCommand());
            _bus.ExpectExactly(_debugPersistentAlivePeer.PeerId, new PingPeerCommand());
            _bus.ExpectExactly(_debugTransientAlivePeer.PeerId, new PingPeerCommand());
        }
        
        [Test]
        public void should_not_ping_until_the_ping_interval_elapsed()
        {
            var startTime = SystemDateTime.UtcNow;
            using (SystemDateTime.Set(utcNow: startTime))
            {
                _detector.DetectDeadPeers();
            }

            using (SystemDateTime.Set(startTime + _pingInterval - 1.Second()))
            {
                _detector.DetectDeadPeers();
            }

            _bus.ExpectExactly(_transientAlivePeer0.PeerId, new PingPeerCommand());
            _bus.ExpectExactly(_transientAlivePeer1.PeerId, new PingPeerCommand());
            _bus.ExpectExactly(_persistentAlivePeer.PeerId, new PingPeerCommand());
            _bus.ExpectExactly(_debugPersistentAlivePeer.PeerId, new PingPeerCommand());
            _bus.ExpectExactly(_debugTransientAlivePeer.PeerId, new PingPeerCommand());
        }

        [Test]
        public void should_not_timeout_if_a_transient_service_responds_to_the_second_ping()
        {
            SetupPeerRepository(_transientAlivePeer0);
            SetupPeerResponse(_transientAlivePeer0.PeerId, false, true);

            using (SystemDateTime.PauseTime())
            {
                var startTime = SystemDateTime.UtcNow;

                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand());

                SystemDateTime.Set(startTime.AddSeconds(_transientPeerTimeout - 1));
                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand(), new PingPeerCommand());

                SystemDateTime.Set(startTime.AddSeconds(_transientPeerTimeout + 1));
                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand(), new PingPeerCommand());
            }
        }

        [Test]
        public void should_not_timeout_if_a_persistent_service_responds_to_the_second_ping()
        {
            SetupPeerRepository(_persistentAlivePeer, _transientAlivePeer0);
            SetupPeerResponse(_transientAlivePeer0.PeerId, true, true);
            SetupPeerResponse(_persistentAlivePeer.PeerId, false, true);

            using (SystemDateTime.PauseTime())
            {
                var startTime = SystemDateTime.UtcNow;

                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand(), new PingPeerCommand());
                _bus.ClearMessages();

                SystemDateTime.Set(startTime.AddSeconds(_transientPeerTimeout - 1));
                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand(), new PingPeerCommand());
                _bus.ClearMessages();

                SystemDateTime.Set(startTime.AddSeconds(_persistentPeerTimeout - 1));
                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand(), new PingPeerCommand());
                _bus.ClearMessages();

                SystemDateTime.Set(startTime.AddSeconds(_persistentPeerTimeout + 1));
                _detector.DetectDeadPeers();
                _bus.ExpectNothing();;
            }
        }

        [Test]
        public void should_not_timeout_if_a_debug_service_responds_to_the_second_ping()
        {
            SetupPeerRepository(_debugPersistentAlivePeer, _debugTransientAlivePeer);
            SetupPeerResponse(_debugPersistentAlivePeer.PeerId, false, true);
            SetupPeerResponse(_debugTransientAlivePeer.PeerId, false, true);
            
            using (SystemDateTime.PauseTime())
            {
                var startTime = SystemDateTime.UtcNow;

                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand(), new PingPeerCommand());
                _bus.ClearMessages();

                SystemDateTime.Set(startTime.AddSeconds(_debugPeerTimeout - 1));
                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand(), new PingPeerCommand());
                _bus.ClearMessages();

                SystemDateTime.Set(startTime.AddSeconds(_debugPeerTimeout + 1));
                _detector.DetectDeadPeers();
                _bus.ExpectNothing();
            }
        }

        [Test]
        public void should_timeout_if_a_transient_service_does_not_respond_in_time()
        {
            SetupPeerRepository(_transientAlivePeer0);
            SetupPeerResponse(_transientAlivePeer0.PeerId, false, false);
           
            using (SystemDateTime.PauseTime())
            {
                var startTime = SystemDateTime.UtcNow;
                var firstPingTimestampUtc = startTime;

                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand());

                SystemDateTime.Set(startTime.AddSeconds(_transientPeerTimeout - 1));
                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand(), new PingPeerCommand());

                SystemDateTime.Set(startTime.AddSeconds(_transientPeerTimeout + 1));
                _detector.DetectDeadPeers();
                _bus.Expect(new UnregisterPeerCommand(_transientAlivePeer0.Peer, firstPingTimestampUtc));
            }
        }
        
        [Test]
        public void should_not_decommission_the_persistence()
        {
            SetupPeerRepository(_persistencePeer);
            SetupPeerResponse(_persistencePeer.PeerId, false, false);

            using (SystemDateTime.PauseTime())
            {
                var startTime = SystemDateTime.UtcNow;

                var persistenceDownDetectedCount = 0;
                _detector.PersistenceDownDetected += () => persistenceDownDetectedCount++;

                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand());

                SystemDateTime.Set(startTime.AddSeconds(_transientPeerTimeout - 1));
                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand(), new PingPeerCommand());

                SystemDateTime.Set(startTime.AddSeconds(_transientPeerTimeout + 1));
                _detector.DetectDeadPeers();

                _bus.ExpectExactly(new PingPeerCommand(), new PingPeerCommand());
                persistenceDownDetectedCount.ShouldEqual(1);
            }
        }

        [Test]
        public void should_timeout_if_a_persistent_service_does_not_respond_in_time()
        {
            SetupPeerRepository(_persistentAlivePeer, _transientAlivePeer0);
            SetupPeerResponse(_transientAlivePeer0.PeerId, true, true);
            SetupPeerResponse(_persistentAlivePeer.PeerId, false, false);
            
            using (SystemDateTime.PauseTime())
            {
                var startTime = SystemDateTime.UtcNow;
                var firstPingTimestampUtc = startTime;

                var peerDownDetectedCount = 0;
                var lastPeerDown = new PeerId(string.Empty);
                var lastPeerDownTimestamp = DateTime.MinValue;
                _detector.PeerDownDetected += (peer, timestamp) =>
                {
                    peerDownDetectedCount++;
                    lastPeerDown = peer;
                    lastPeerDownTimestamp = timestamp;
                };

                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand(), new PingPeerCommand());
                _bus.ClearMessages();
            
                SystemDateTime.Set(startTime.AddSeconds(_transientPeerTimeout - 1));
                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand(), new PingPeerCommand());
                _bus.ClearMessages();

                var retryTimestamp = startTime.AddSeconds(_persistentPeerTimeout - 1);
                SystemDateTime.Set(retryTimestamp);
                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand(), new PingPeerCommand());
                _bus.ClearMessages();
            
                SystemDateTime.Set(startTime.AddSeconds(_persistentPeerTimeout + 1));
                _detector.DetectDeadPeers();
                peerDownDetectedCount.ShouldEqual(1);
                lastPeerDown.ShouldEqualDeeply(_persistentAlivePeer.Peer.Id);
                lastPeerDownTimestamp.ShouldEqual(firstPingTimestampUtc);
            }
        }

        [Test]
        public void should_raise_PeerResponding_when_peer_starts_replying_again_to_ping()
        {
            SetupPeerRepository(_persistentAlivePeer);
            SetupPeerResponse(_persistentAlivePeer.PeerId, false, false, false, true);

            using (SystemDateTime.PauseTime())
            {
                var startTime = SystemDateTime.UtcNow;
                var firstPingTimestampUtc = startTime;

                var peerDownDetectedCount = 0;
                var lastPeerDown = new PeerId(string.Empty);
                var lastPeerDownTimestamp = DateTime.MinValue;
                _detector.PeerDownDetected += (peer, timestamp) =>
                {
                    peerDownDetectedCount++;
                    lastPeerDown = peer;
                    lastPeerDownTimestamp = timestamp;
                };

                var peerUpDetectedCount = 0;
                var lastPeerUp = new PeerId(string.Empty);
                var lastPeerUpTimestamp = DateTime.MinValue;
                _detector.PeerRespondingDetected += (peer, timestamp) =>
                {
                    peerUpDetectedCount++;
                    lastPeerUp = peer;
                    lastPeerUpTimestamp = timestamp;
                };

                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand());
                _bus.ClearMessages();

                SystemDateTime.Set(startTime.AddSeconds(_persistentPeerTimeout - 1));
                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand());
                _bus.ClearMessages();

                SystemDateTime.Set(startTime.AddSeconds(_persistentPeerTimeout + 1));
                _detector.DetectDeadPeers();
                peerDownDetectedCount.ShouldEqual(1);
                lastPeerDown.ShouldEqualDeeply(_persistentAlivePeer.Peer.Id);
                lastPeerDownTimestamp.ShouldEqual(firstPingTimestampUtc);

                // simulate MarkPeerAsNotRespondingCommand handler
                _persistentAlivePeer.Peer.IsResponding = false;

                SystemDateTime.Set(SystemDateTime.Now.Add(_pingInterval));
                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand());
                _bus.ClearMessages();

                SystemDateTime.Set(SystemDateTime.Now.Add(_pingInterval));
                _detector.DetectDeadPeers();
                peerUpDetectedCount.ShouldEqual(1);
                lastPeerUp.ShouldEqualDeeply(_persistentAlivePeer.Peer.Id);
                lastPeerUpTimestamp.ShouldEqual(SystemDateTime.UtcNow);
            }
        }

        [Test]
        public void should_timeout_if_any_debug_service_does_not_respond_in_time()
        {
            SetupPeerRepository(_debugPersistentAlivePeer, _debugTransientAlivePeer);
            SetupPeerResponse(_debugPersistentAlivePeer.PeerId, false, false);
            SetupPeerResponse(_debugTransientAlivePeer.PeerId, false, false);

            using (SystemDateTime.PauseTime())
            {
                var startTime = SystemDateTime.UtcNow;
                var firstPingTimestampUtc = startTime;

                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand(), new PingPeerCommand());
                _bus.ClearMessages();

                SystemDateTime.Set(startTime.AddSeconds(_debugPeerTimeout - 1));
                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand(), new PingPeerCommand());
                _bus.ClearMessages();

                SystemDateTime.Set(startTime.AddSeconds(_debugPeerTimeout + 1));
                _detector.DetectDeadPeers();
                _bus.ExpectExactly(
                    new UnregisterPeerCommand(_debugTransientAlivePeer.Peer, firstPingTimestampUtc),
                    new UnregisterPeerCommand(_debugPersistentAlivePeer.Peer, firstPingTimestampUtc)
                    );
            }
        }

        private void SetupPeerRepository(params PeerDescriptor[] peer)
        {
            _peerRepositoryMock.Setup(repo => repo.GetPeers()).Returns(new List<PeerDescriptor>(peer));
        }


        private void SetupPeerResponse(PeerId peerId, params bool[] respondToPing)
        {
            var invocationCount = 0;
            _bus.AddHandlerForPeer<PingPeerCommand>(peerId, cmd =>
            {
                var shouldRespond = invocationCount < respondToPing.Length && respondToPing[invocationCount];
                ++invocationCount;
                if(shouldRespond)
                    return true;

                throw new InvalidOperationException();
            });
        }

        private static PeerDescriptor CreatePeerDescriptor(string peerId, bool isPersistent, bool isUp, bool hasDebuggerAttached)
        {
            var descriptor = new Peer(new PeerId(peerId), "tcp://abcdell348:58920", isUp).ToPeerDescriptor(isPersistent);
            descriptor.HasDebuggerAttached = hasDebuggerAttached;

            return descriptor;
        }

        /// <summary>
        /// Executes handler synchronously but hangs on exceptions
        /// </summary>
        public class HangOnThrowHandlerExecutor : TestBus.IHandlerExecutor
        {
            public Task<CommandResult> Execute(ICommand command, Func<IMessage, object> handler)
            {
                var taskCompletionSource = new TaskCompletionSource<CommandResult>();
                try
                {
                    var result = handler != null ? handler(command) : null;
                    taskCompletionSource.SetResult(new CommandResult(0, result));
                }
                catch (Exception)
                {
                    return new TaskCompletionSource<CommandResult>().Task;
                }
             
                return taskCompletionSource.Task;
            }
        }
    }
}