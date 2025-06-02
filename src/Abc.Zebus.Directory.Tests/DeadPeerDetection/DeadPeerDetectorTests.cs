using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Directory.Configuration;
using Abc.Zebus.Directory.DeadPeerDetection;
using Abc.Zebus.Directory.Messages;
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
        private PeerDescriptor _directoryPeer;
        private string[] _peersNotToDecommission;

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
            _directoryPeer = CreatePeerDescriptor("NonStandardDirectoryName", isUp: true, isPersistent: false, hasDebuggerAttached: false);
            _directoryPeer.Subscriptions = new[] { new Subscription(new MessageTypeId(typeof(RegisterPeerCommand))) };
            _peersNotToDecommission = new string[0];

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
            _peerRepositoryMock.Setup(repo => repo.GetPeers(It.IsAny<bool>())).Returns(peerDescriptors);
            _peerRepositoryMock.Setup(repo => repo.Get(It.IsAny<PeerId>())).Returns<PeerId>(peerId => peerDescriptors.FirstOrDefault(x => x.Peer.Id == peerId));

            _bus = new TestBus();

            _configurationMock = new Mock<IDirectoryConfiguration>();
            _configurationMock.As<IDirectoryConfiguration>().SetupGet(conf => conf.TransientPeerPingTimeout).Returns(_transientPeerTimeout.Seconds());
            _configurationMock.As<IDirectoryConfiguration>().SetupGet(conf => conf.PersistentPeerPingTimeout).Returns(_persistentPeerTimeout.Seconds());
            _configurationMock.As<IDirectoryConfiguration>().SetupGet(conf => conf.DebugPeerPingTimeout).Returns(_debugPeerTimeout.Seconds());
            _configurationMock.As<IDirectoryConfiguration>().SetupGet(conf => conf.PeerPingInterval).Returns(_pingInterval);
            _configurationMock.As<IDirectoryConfiguration>().SetupGet(conf => conf.WildcardsForPeersNotToDecommissionOnTimeout).Returns(() => _peersNotToDecommission);

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
        public void forget_decommissioned_peers()
        {
            _detector.DetectDeadPeers();

            SetupPeerRepository(_persistentAlivePeer);

            _detector.DetectDeadPeers();

            _detector.KnownPeerIds.ShouldBeEquivalentTo(new[] { _persistentAlivePeer.PeerId });
        }

        [Test]
        public void should_not_ping_until_the_ping_interval_elapsed()
        {
            var startTime = SystemDateTime.UtcNow;
            using (SystemDateTime.PauseTime(startTime))
            {
                _detector.DetectDeadPeers();
            }

            using (SystemDateTime.PauseTime(startTime + _pingInterval - 1.Second()))
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
            SetupPeerResponses(_transientAlivePeer0.PeerId, false, true);

            using (SystemDateTime.PauseTime())
            {
                var startTime = SystemDateTime.UtcNow;

                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand());

                SystemDateTime.PauseTime(startTime.AddSeconds(_transientPeerTimeout - 1));
                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand(), new PingPeerCommand());

                SystemDateTime.PauseTime(startTime.AddSeconds(_transientPeerTimeout + 1));
                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand(), new PingPeerCommand());
            }
        }

        [Test]
        public void should_not_timeout_if_a_persistent_service_responds_to_the_second_ping()
        {
            SetupPeerRepository(_persistentAlivePeer, _transientAlivePeer0);
            SetupPeerResponses(_transientAlivePeer0.PeerId, true, true);
            SetupPeerResponses(_persistentAlivePeer.PeerId, false, true);

            using (SystemDateTime.PauseTime())
            {
                var startTime = SystemDateTime.UtcNow;

                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand(), new PingPeerCommand());
                _bus.ClearMessages();

                SystemDateTime.PauseTime(startTime.AddSeconds(_transientPeerTimeout - 1));
                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand(), new PingPeerCommand());
                _bus.ClearMessages();

                SystemDateTime.PauseTime(startTime.AddSeconds(_persistentPeerTimeout - 1));
                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand(), new PingPeerCommand());
                _bus.ClearMessages();

                SystemDateTime.PauseTime(startTime.AddSeconds(_persistentPeerTimeout + 1));
                _detector.DetectDeadPeers();
                _bus.ExpectNothing();;
            }
        }

        [Test]
        public void should_not_timeout_if_a_debug_service_responds_to_the_second_ping()
        {
            SetupPeerRepository(_debugPersistentAlivePeer, _debugTransientAlivePeer);
            SetupPeerResponses(_debugPersistentAlivePeer.PeerId, false, true);
            SetupPeerResponses(_debugTransientAlivePeer.PeerId, false, true);

            using (SystemDateTime.PauseTime())
            {
                var startTime = SystemDateTime.UtcNow;

                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand(), new PingPeerCommand());
                _bus.ClearMessages();

                SystemDateTime.PauseTime(startTime.AddSeconds(_debugPeerTimeout - 1));
                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand(), new PingPeerCommand());
                _bus.ClearMessages();

                SystemDateTime.PauseTime(startTime.AddSeconds(_debugPeerTimeout + 1));
                _detector.DetectDeadPeers();
                _bus.ExpectNothing();
            }
        }

        [Test]
        public void should_timeout_if_a_transient_service_does_not_respond_in_time()
        {
            SetupPeerRepository(_transientAlivePeer0);
            SetupPeerResponses(_transientAlivePeer0.PeerId, false, false);

            using (SystemDateTime.PauseTime())
            {
                var startTime = SystemDateTime.UtcNow;
                var firstPingTimestampUtc = startTime;

                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand());

                SystemDateTime.PauseTime(startTime.AddSeconds(_transientPeerTimeout - 1));
                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand(), new PingPeerCommand());

                SystemDateTime.PauseTime(startTime.AddSeconds(_transientPeerTimeout + 1));
                _detector.DetectDeadPeers();
                _bus.Expect(new UnregisterPeerCommand(_transientAlivePeer0.Peer, firstPingTimestampUtc));
            }
        }

        private static readonly string[] _peersNotToDecommissionExamples =
        {
            "Abc.TransientAlive.0",
            "Abc.TransientAlive.*",
            "*Abc.TransientA*",
            "Abc.TransientAlive.?",
        };

        [Test]
        [TestCaseSource(nameof(_peersNotToDecommissionExamples))]
        public void should_mark_as_not_responding_if_a_transient_peer_in_the_whitelist_does_not_respond_in_time(string peerNotToDecommission)
        {
            _peersNotToDecommission = new[] { peerNotToDecommission };
            SetupPeerRepository(_transientAlivePeer0);
            SetupPeerResponses(_transientAlivePeer0.PeerId, false, false);

            using (SystemDateTime.PauseTime())
            {
                var startTime = SystemDateTime.UtcNow;
                var firstPingTimestampUtc = startTime;

                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand());
                _bus.ClearMessages();

                SystemDateTime.PauseTime(startTime.AddSeconds(_transientPeerTimeout - 1));
                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand());
                _bus.ClearMessages();

                SystemDateTime.PauseTime(startTime.AddSeconds(_transientPeerTimeout + 1));
                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new MarkPeerAsNotRespondingInternalCommand(_transientAlivePeer0.Peer.Id, firstPingTimestampUtc));
            }
        }

        [Test]
        public void should_timeout_if_a_persistent_service_does_not_respond_in_time()
        {
            SetupPeerRepository(_persistentAlivePeer, _transientAlivePeer0);
            SetupPeerResponses(_transientAlivePeer0.PeerId, true, true);
            SetupPeerResponses(_persistentAlivePeer.PeerId, false, false);

            using (SystemDateTime.PauseTime())
            {
                var startTime = SystemDateTime.UtcNow;
                var firstPingTimestampUtc = startTime;

                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand(), new PingPeerCommand());
                _bus.ClearMessages();

                SystemDateTime.PauseTime(startTime.AddSeconds(_transientPeerTimeout - 1));
                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand(), new PingPeerCommand());
                _bus.ClearMessages();

                var retryTimestamp = startTime.AddSeconds(_persistentPeerTimeout - 1);
                SystemDateTime.PauseTime(retryTimestamp);
                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand(), new PingPeerCommand());
                _bus.ClearMessages();

                SystemDateTime.PauseTime(startTime.AddSeconds(_persistentPeerTimeout + 1));
                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new MarkPeerAsNotRespondingInternalCommand(_persistentAlivePeer.Peer.Id, firstPingTimestampUtc));
            }
        }

        [Test]
        public void should_not_decommission_directory_peer()
        {
            SetupPeerRepository(_directoryPeer, _transientAlivePeer0);
            SetupPeerResponses(_transientAlivePeer0.PeerId, false, false);
            SetupPeerResponses(_directoryPeer.PeerId, false, false);

            using (SystemDateTime.PauseTime())
            {
                var startTime = SystemDateTime.UtcNow;
                var firstPingTimestampUtc = startTime;

                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand());
                _bus.ClearMessages();

                SystemDateTime.PauseTime(startTime.AddSeconds(_transientPeerTimeout - 1));
                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand());
                _bus.ClearMessages();

                SystemDateTime.PauseTime(startTime.AddSeconds(_transientPeerTimeout + 1));
                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new UnregisterPeerCommand(_transientAlivePeer0.Peer, firstPingTimestampUtc));
            }
        }

        [Test]
        public void should_not_decommission_the_persistence()
        {
            SetupPeerRepository(_persistencePeer);
            SetupPeerResponses(_persistencePeer.PeerId, false, false);

            using (SystemDateTime.PauseTime())
            {
                var startTime = SystemDateTime.UtcNow;

                var persistenceDownDetectedCount = 0;
                _detector.PersistenceDownDetected += () => persistenceDownDetectedCount++;

                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand());

                SystemDateTime.PauseTime(startTime.AddSeconds(_transientPeerTimeout - 1));
                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand(), new PingPeerCommand());

                SystemDateTime.PauseTime(startTime.AddSeconds(_transientPeerTimeout + 1));
                _detector.DetectDeadPeers();

                _bus.ExpectExactly(new PingPeerCommand(), new PingPeerCommand());
                persistenceDownDetectedCount.ShouldEqual(1);
            }
        }

        private class PeerEvent
        {
            public PeerId PeerId { get; private set; }
            public DateTime Timestamp { get; private set; }

            public PeerEvent(PeerId peerId, DateTime timestamp)
            {
                PeerId = peerId;
                Timestamp = timestamp;
            }
        }

        [Test]
        public void should_raise_PingMissed_before_a_peer_is_marked_as_timed_out()
        {
            SetupPeerRepository(_persistentAlivePeer, _transientAlivePeer0);
            SetupPeerResponses(_transientAlivePeer0.PeerId, true, true);
            SetupPeerResponses(_persistentAlivePeer.PeerId, false, false);

            using (SystemDateTime.PauseTime())
            {
                var missedPings = new List<PeerEvent>();
                _detector.PingTimeout += (peer, timestamp) => missedPings.Add(new PeerEvent(peer, timestamp));

                var startTime = SystemDateTime.UtcNow;
                _detector.DetectDeadPeers();

                SystemDateTime.PauseTime(startTime.Add(_pingInterval - 1.Second()));
                _detector.DetectDeadPeers();
                missedPings.Count.ShouldEqual(0);

                SystemDateTime.PauseTime(startTime.Add(_pingInterval + 1.Second()));
                _detector.DetectDeadPeers();
                missedPings.Count.ShouldEqual(1);
                missedPings.First().PeerId.ShouldEqual(_persistentAlivePeer.PeerId);

                SystemDateTime.PauseTime(startTime.Add(_pingInterval + _pingInterval + 1.Second()));
                _detector.DetectDeadPeers();
                missedPings.Count.ShouldEqual(2);
                missedPings.All(evt => evt.PeerId == _persistentAlivePeer.PeerId).ShouldBeTrue();
            }
        }

        [Test]
        public void should_raise_PeerResponding_when_peer_starts_replying_again_to_ping()
        {
            SetupPeerRepository(_persistentAlivePeer);
            SetupPeerResponses(_persistentAlivePeer.PeerId, false, false, false, true);

            using (SystemDateTime.PauseTime())
            {
                var startTime = SystemDateTime.UtcNow;
                var firstPingTimestampUtc = startTime;

                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand());
                _bus.ClearMessages();

                SystemDateTime.PauseTime(startTime.AddSeconds(_persistentPeerTimeout - 1));
                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand());
                _bus.ClearMessages();

                SystemDateTime.PauseTime(startTime.AddSeconds(_persistentPeerTimeout + 1));
                _detector.DetectDeadPeers();
                _bus.Expect(new MarkPeerAsNotRespondingInternalCommand(_persistentAlivePeer.Peer.Id, firstPingTimestampUtc));
                _bus.ClearMessages();

                // simulate MarkPeerAsNotRespondingCommand handler
                _persistentAlivePeer.Peer.IsResponding = false;

                SystemDateTime.PauseTime(SystemDateTime.UtcNow.Add(_pingInterval));
                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand());
                _bus.ClearMessages();

                SystemDateTime.PauseTime(SystemDateTime.UtcNow.Add(_pingInterval));
                _detector.DetectDeadPeers();
                _bus.Expect(new MarkPeerAsRespondingInternalCommand(_persistentAlivePeer.Peer.Id, _persistentAlivePeer.TimestampUtc.Value + 1.Millisecond()));
            }
        }

        [Test]
        public void should_timeout_if_any_debug_service_does_not_respond_in_time()
        {
            SetupPeerRepository(_debugPersistentAlivePeer, _debugTransientAlivePeer);
            SetupPeerResponses(_debugPersistentAlivePeer.PeerId, false, false);
            SetupPeerResponses(_debugTransientAlivePeer.PeerId, false, false);

            using (SystemDateTime.PauseTime())
            {
                var startTime = SystemDateTime.UtcNow;
                var firstPingTimestampUtc = startTime;

                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand(), new PingPeerCommand());
                _bus.ClearMessages();

                SystemDateTime.PauseTime(startTime.AddSeconds(_debugPeerTimeout - 1));
                _detector.DetectDeadPeers();
                _bus.ExpectExactly(new PingPeerCommand(), new PingPeerCommand());
                _bus.ClearMessages();

                SystemDateTime.PauseTime(startTime.AddSeconds(_debugPeerTimeout + 1));
                _detector.DetectDeadPeers();
                _bus.ExpectExactly(
                    new UnregisterPeerCommand(_debugTransientAlivePeer.Peer, firstPingTimestampUtc),
                    new UnregisterPeerCommand(_debugPersistentAlivePeer.Peer, firstPingTimestampUtc)
                    );
            }
        }

        [Test]
        public void should_ping_peers_on_peer_repository_timeout()
        {
            using (SystemDateTime.PauseTime())
            {
                // 1: Repository is operational
                SetupPeerRepository(_transientAlivePeer0, _transientAlivePeer1);
                SetupPeerResponse(_transientAlivePeer0.PeerId, true);
                SetupPeerResponse(_transientAlivePeer1.PeerId, true);

                _detector.DetectDeadPeers();

                _bus.ExpectExactly(PingPeerCommands(2));

                SystemDateTime.AddToPausedTime(_transientPeerTimeout.Seconds());

                _detector.DetectDeadPeers();

                _bus.ExpectExactly(PingPeerCommands(4));

                // 2: Repository is broken
                SetupPeerRepository(() => new TimeoutException("The task didn't complete before timeout."));

                SystemDateTime.AddToPausedTime(_transientPeerTimeout.Seconds());

                _detector.DetectDeadPeers();

                _bus.ExpectExactly(PingPeerCommands(6));

                SystemDateTime.AddToPausedTime(_transientPeerTimeout.Seconds());

                // 3: Peer0 stops responding to pings
                SetupPeerResponse(_transientAlivePeer0.PeerId, false);

                _detector.DetectDeadPeers();

                _bus.ExpectExactly(PingPeerCommands(8));

                var firstUnansweredPingTimestampUtc = SystemDateTime.UtcNow;

                SystemDateTime.AddToPausedTime(_transientPeerTimeout.Seconds());

                _detector.DetectDeadPeers();

                _bus.ExpectExactly(PingPeerCommands(10));

                SystemDateTime.AddToPausedTime(_transientPeerTimeout.Seconds());

                // 4: Repository is operational again
                SetupPeerRepository(_transientAlivePeer0, _transientAlivePeer1);

                _detector.DetectDeadPeers();

                _bus.ExpectExactly(PingPeerCommands(11).Append(new UnregisterPeerCommand(_transientAlivePeer0.Peer, firstUnansweredPingTimestampUtc)).ToArray());
            }
        }

        [Test]
        public void should_ignore_ping_response_on_removed_peer()
        {
            _bus.HandlerExecutor = new TestBus.AsyncHandlerExecutor();

            SetupPeerRepository(_persistentAlivePeer);

            var pingTaskCompletionSources = new List<TaskCompletionSource<object>>();
            _bus.AddHandler<PingPeerCommand>(cmd =>
            {
                var taskCompletionSource = new TaskCompletionSource<object>();
                pingTaskCompletionSources.Add(taskCompletionSource);

                taskCompletionSource.Task.Wait();
            });

            using (SystemDateTime.PauseTime())
            {
                // Initial ping
                var initialTime = SystemDateTime.UtcNow;
                _detector.DetectDeadPeers();
                Wait.Until(() => pingTaskCompletionSources.Count == 1, 1.Second());

                // Peer marked as not responding
                SystemDateTime.AddToPausedTime(_persistentPeerTimeout.Seconds());
                _detector.DetectDeadPeers();
                _bus.Expect(new MarkPeerAsNotRespondingInternalCommand(_persistentAlivePeer.PeerId, initialTime));

                // Peer removed
                SetupPeerRepository();
                _bus.ClearMessages();
                _detector.DetectDeadPeers();

                // Ping ack
                pingTaskCompletionSources[0].SetResult(null);

                // Task completions are processed asynchronously, so the sleep is required.
                Thread.Sleep(100);

                _bus.ExpectNothing();
            }
        }

        private IMessage[] PingPeerCommands(int count)
        {
            return Enumerable.Range(0, count).Select(x => (IMessage)new PingPeerCommand()).ToArray();
        }

        private void SetupPeerRepository(params PeerDescriptor[] peer)
        {
            _peerRepositoryMock.Setup(repo => repo.GetPeers(It.IsAny<bool>())).Returns(new List<PeerDescriptor>(peer));
        }

        private void SetupPeerRepository(Func<Exception> exception)
        {
            _peerRepositoryMock.Setup(repo => repo.GetPeers(It.IsAny<bool>())).Throws(exception);
        }

        private void SetupPeerResponses(PeerId peerId, params bool[] respondToPing)
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

        private void SetupPeerResponse(PeerId peerId, bool respondToPing)
        {
            _bus.AddHandlerForPeer<PingPeerCommand>(peerId, cmd =>
            {
                if(respondToPing)
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
                    taskCompletionSource.SetResult(new CommandResult(0, string.Empty, result));
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
