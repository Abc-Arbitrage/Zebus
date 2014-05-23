using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Directory.Configuration;
using Abc.Zebus.Directory.Storage;
using Abc.Zebus.Util;
using log4net;

namespace Abc.Zebus.Directory.DeadPeerDetection
{
    public class DeadPeerDetector
    {
        private readonly ConcurrentDictionary<PeerId, DeadPeerDetectorEntry> _peers = new ConcurrentDictionary<PeerId, DeadPeerDetectorEntry>();
        private readonly IBus _bus;
        private readonly IPeerRepository _peerRepository;
        private readonly IDirectoryConfiguration _configuration;
        private DateTime? _lastPingTimeUtc;
        private TimeSpan _period;
        private static readonly ILog _logger = LogManager.GetLogger(typeof(DeadPeerDetector));
        private bool _isRunning;
        private Thread _thread;
        private int _exceptionCount;
        private const int _exceptionCountMax = 10;

        public DeadPeerDetector(IBus bus, IPeerRepository peerRepository, IDirectoryConfiguration configuration) : this(bus, peerRepository, configuration, 5.Seconds()) { }

        public DeadPeerDetector(IBus bus, IPeerRepository peerRepository, IDirectoryConfiguration configuration, TimeSpan period )
        {
            _bus = bus;
            _peerRepository = peerRepository;
            _configuration = configuration;
            _period = period;

            TaskScheduler = TaskScheduler.Current;
        }

        public event Action PersistenceDownDetected = delegate { };

        public event Action<PeerId, DateTime> PeerDownDetected = delegate { };

        public event Action<PeerId, DateTime> PeerRespondingDetected = delegate { };

        public TaskScheduler TaskScheduler { get; set; }

        public void DoPeriodicAction()
        {
            var peers = _peerRepository.GetPeers().Select(ToPeerEntry).ToList();
            ProcessPeerEntries(peers);
        }

        private DeadPeerDetectorEntry ToPeerEntry(PeerDescriptor descriptor)
        {
            var peer = _peers.GetOrAdd(descriptor.PeerId, x =>
            {
                var e = new DeadPeerDetectorEntry(descriptor, _configuration, _bus, TaskScheduler);
                e.PeerTimeoutDetected += OnPeerTimeout;
                e.PeerRespondingDetected += OnPeerResponding;

                return e;
            });
            peer.Descriptor = descriptor;

            return peer;
        }

        public void OnPeerTimeout(DeadPeerDetectorEntry entry, DateTime timeoutTimestampUtc)
        {
            var descriptor = entry.Descriptor;
            if (descriptor.PeerId.IsPersistence())
            {
                PersistenceDownDetected();
                
                return;
            }

            var canPeerBeUnregistered = !descriptor.IsPersistent || descriptor.HasDebuggerAttached;
            if (canPeerBeUnregistered)
            {
                _bus.Send(new UnregisterPeerCommand(descriptor.Peer, timeoutTimestampUtc));
            }
            else if (descriptor.Peer.IsResponding)
            {
                PeerDownDetected(descriptor.PeerId, timeoutTimestampUtc);
                
                descriptor.Peer.IsResponding = false;
            }
        }

        public void OnPeerResponding(DeadPeerDetectorEntry entry,DateTime timeoutTimestampUtc)
        {
            PeerRespondingDetected(entry.Descriptor.PeerId, timeoutTimestampUtc);
        }

        private void ProcessPeerEntries(IEnumerable<DeadPeerDetectorEntry> peers)
        {
            var timestampUtc = SystemDateTime.UtcNow;
            var shouldSendPing = ShouldSendPing(timestampUtc);
            if (shouldSendPing)
                _lastPingTimeUtc = timestampUtc;

            foreach (var peer in peers.Where(x => x.IsUp))
            {
                peer.Process(timestampUtc, shouldSendPing);
            }
        }

        private bool ShouldSendPing(DateTime timestampUtc)
        {
            if (_lastPingTimeUtc == null)
                return true;

            var elapsedSinceLastPing = timestampUtc - _lastPingTimeUtc.Value;
            return elapsedSinceLastPing >= _configuration.PeerPingInterval;
        }


        public void AfterStart()
        {
            if (_period == TimeSpan.MaxValue)
            {
                _logger.InfoFormat("Periodic action disabled");
                return;
            }

            _isRunning = true;
            _thread = new Thread(MainLoop) { Name = GetType().Name + "MainLoop" };
            _thread.Start();
        }

        public void BeforeStop()
        {
            if (!_isRunning)
                return;

            _isRunning = false;
            if (!_thread.Join(2000))
                _logger.Warn("Unable to terminate periodic action");
        }

        private void MainLoop()
        {
            var sleep = (int)Math.Min(300, _period.TotalMilliseconds / 2);

            _logger.InfoFormat("MainLoop started, Period: {0}ms, Sleep: {1}ms", _period.TotalMilliseconds, sleep);

            var next = DateTime.UtcNow + _period;
            while (_isRunning)
            {
                if (DateTime.UtcNow >= next)
                    InvokePeriodicAction(ref next);
                else
                    Thread.Sleep(sleep);
            }
            _logger.Info("MainLoop stopped");
        }

        private void InvokePeriodicAction(ref DateTime next)
        {
            try
            {
                DoPeriodicAction();
                _exceptionCount = 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                ++_exceptionCount;

                PublishError(ex);
            }
            if (_exceptionCount >= _exceptionCountMax)
            {
                _logger.ErrorFormat("Too many exceptions, periodic action paused (2 min)");
                next = next + _period + TimeSpan.FromMinutes(2);
            }
            else
            {
                next = next + _period;
            }
        }

        public Action<Exception> PublishError { get; set; }
    }
}