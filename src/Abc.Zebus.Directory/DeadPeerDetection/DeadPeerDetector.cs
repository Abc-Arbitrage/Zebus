using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Directory.Configuration;
using Abc.Zebus.Directory.Storage;
using Abc.Zebus.Util;
using Abc.Zebus.Util.Extensions;
using log4net;

namespace Abc.Zebus.Directory.DeadPeerDetection
{
    public class DeadPeerDetector : IDeadPeerDetector
    {
        private readonly Dictionary<PeerId, DeadPeerDetectorEntry> _peers = new Dictionary<PeerId, DeadPeerDetectorEntry>();
        private readonly ILog _logger = LogManager.GetLogger(typeof(DeadPeerDetector));
        private readonly IBus _bus;
        private readonly IPeerRepository _peerRepository;
        private readonly IDirectoryConfiguration _configuration;
        private readonly TimeSpan _detectionPeriod = 5.Seconds();
        private Thread _detectionThread;
        private DateTime? _lastPingTimeUtc;
        private bool _isRunning;

        public DeadPeerDetector(IBus bus, IPeerRepository peerRepository, IDirectoryConfiguration configuration)
        {
            _bus = bus;
            _peerRepository = peerRepository;
            _configuration = configuration;

            TaskScheduler = TaskScheduler.Current;
            ExceptionHandler = ex => _logger.ErrorFormat("MainLoop error: {0}", ex);
        }

        public event Action PersistenceDownDetected = delegate { };
        public event Action<PeerId, DateTime> PeerDownDetected = delegate { };
        public event Action<PeerId, DateTime> PingMissed = delegate { };
        public event Action<PeerId, DateTime> PeerRespondingDetected = delegate { };

        public TaskScheduler TaskScheduler { get; set; }
        public Action<Exception> ExceptionHandler { get; set; }

        internal void DetectDeadPeers()
        {
            var timestampUtc = SystemDateTime.UtcNow;
            var entries = _peerRepository.GetPeers(loadDynamicSubscriptions: false).Select(ToPeerEntry).ToList();
            
            var shouldSendPing = ShouldSendPing(timestampUtc);
            if (shouldSendPing)
                _lastPingTimeUtc = timestampUtc;

            foreach (var entry in entries.Where(x => x.IsUp))
            {
                entry.Process(timestampUtc, shouldSendPing);
            }
        }

        private DeadPeerDetectorEntry ToPeerEntry(PeerDescriptor descriptor)
        {
            var peer = _peers.GetValueOrAdd(descriptor.PeerId, () => CreateEntry(descriptor));
            peer.Descriptor = descriptor;

            return peer;
        }

        private DeadPeerDetectorEntry CreateEntry(PeerDescriptor descriptor)
        {
            var entry = new DeadPeerDetectorEntry(descriptor, _configuration, _bus, TaskScheduler);
            entry.PeerTimeoutDetected += OnPeerTimeout;
            entry.PeerRespondingDetected += OnPeerResponding;
            entry.PingMissed += (detectorEntry, time) => PingMissed(detectorEntry.Descriptor.PeerId, time);

            return entry;
        }

        private void OnPeerTimeout(DeadPeerDetectorEntry entry, DateTime timeoutTimestampUtc)
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

        private void OnPeerResponding(DeadPeerDetectorEntry entry,DateTime timeoutTimestampUtc)
        {
            PeerRespondingDetected(entry.Descriptor.PeerId, timeoutTimestampUtc);
        }

        private bool ShouldSendPing(DateTime timestampUtc)
        {
            if (_lastPingTimeUtc == null)
                return true;

            var elapsedSinceLastPing = timestampUtc - _lastPingTimeUtc.Value;
            return elapsedSinceLastPing >= _configuration.PeerPingInterval;
        }

        public void Start()
        {
            _isRunning = true;
            _detectionThread = new Thread(MainLoop) { Name = GetType().Name + "MainLoop" };
            _detectionThread.Start();
        }

        public void Stop()
        {
            _isRunning = false;
            if (!_detectionThread.Join(2000))
                _logger.Warn("Unable to terminate MainLoop");
        }

        void IDisposable.Dispose()
        {
            if (_isRunning)
                Stop();
        }

        private void MainLoop()
        {
            _logger.InfoFormat("MainLoop started");

            var next = DateTime.UtcNow + _detectionPeriod;
            while (_isRunning)
            {
                var utcNow = DateTime.UtcNow;
                if (utcNow < next)
                {
                    Thread.Sleep(300);
                    continue;
                }

                next += _detectionPeriod;

                try
                {
                    DetectDeadPeers();
                }
                catch (Exception ex)
                {
                    ExceptionHandler(ex);
                }
            }
            _logger.Info("MainLoop stopped");
        }
    }
}