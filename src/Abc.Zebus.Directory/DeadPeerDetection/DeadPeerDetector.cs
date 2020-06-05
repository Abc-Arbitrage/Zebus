using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
        private static readonly TimeSpan _commandTimeout = 5.Seconds();
        private static readonly ILog _logger = LogManager.GetLogger(typeof(DeadPeerDetector));
        private readonly Dictionary<PeerId, DeadPeerDetectorEntry> _peers = new Dictionary<PeerId, DeadPeerDetectorEntry>();
        private readonly IBus _bus;
        private readonly IPeerRepository _peerRepository;
        private readonly IDirectoryConfiguration _configuration;
        private readonly TimeSpan _detectionPeriod = 5.Seconds();
        private Thread? _detectionThread;
        private DateTime? _lastPingTimeUtc;
        private bool _isRunning;

        public DeadPeerDetector(IBus bus, IPeerRepository peerRepository, IDirectoryConfiguration configuration)
        {
            _bus = bus;
            _peerRepository = peerRepository;
            _configuration = configuration;

            TaskScheduler = TaskScheduler.Current;
        }

        public event Action<Exception>? Error;
        public event Action? PersistenceDownDetected;
        public event Action<PeerId, DateTime>? PingTimeout;

        public TaskScheduler TaskScheduler { get; set; }
        public IEnumerable<PeerId> KnownPeerIds => _peers.Keys;

        internal void DetectDeadPeers()
        {
            var timestampUtc = SystemDateTime.UtcNow;
            var entries = _peerRepository.GetPeers(loadDynamicSubscriptions: false)
                                         .Where(peer => peer.Subscriptions.All(sub => sub.MessageTypeId != MessageUtil.TypeId<RegisterPeerCommand>()))
                                         .Select(ToPeerEntry)
                                         .ToList();

            var shouldSendPing = ShouldSendPing(timestampUtc);
            if (shouldSendPing)
                _lastPingTimeUtc = timestampUtc;

            foreach (var entry in entries.Where(x => x.IsUp))
            {
                entry.Process(timestampUtc, shouldSendPing);
            }

            var decommissionedPeerIds = _peers.Keys.Except(entries.Select(x => x.Descriptor.PeerId)).ToList();
            _peers.RemoveRange(decommissionedPeerIds);
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
            entry.PingTimeout += (detectorEntry, time) => PingTimeout?.Invoke(detectorEntry.Descriptor.PeerId, time);

            return entry;
        }

        private void OnPeerTimeout(DeadPeerDetectorEntry entry, DateTime timeoutTimestampUtc)
        {
            var descriptor = entry.Descriptor;
            if (descriptor.PeerId.IsPersistence())
            {
                PersistenceDownDetected?.Invoke();
                return;
            }

            var canPeerBeUnregistered = (!descriptor.IsPersistent || descriptor.HasDebuggerAttached) && IsNotInTheProtectedList(descriptor);
            if (canPeerBeUnregistered)
            {
                _bus.Send(new UnregisterPeerCommand(descriptor.Peer, timeoutTimestampUtc));
            }
            else if (descriptor.Peer.IsResponding)
            {
                _bus.Send(new MarkPeerAsNotRespondingCommand(descriptor.PeerId, timeoutTimestampUtc)).Wait(_commandTimeout);
                descriptor.Peer.IsResponding = false;
            }
        }

        private bool IsNotInTheProtectedList(PeerDescriptor descriptor)
        {
            var peerId = descriptor.PeerId.ToString();

            foreach (var wildcardPattern in _configuration.WildcardsForPeersNotToDecommissionOnTimeout ?? Array.Empty<string>())
            {
                var pattern = Regex.Escape(wildcardPattern.Trim());
                pattern = pattern.Replace(@"\?", ".");
                pattern = pattern.Replace(@"\*", ".*?");
                pattern = pattern.Replace(@"\#", "[0-9]");
                pattern = "^" + pattern + "$";

                if (Regex.IsMatch(peerId, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                    return false;
            }

            return true;
        }

        private void OnPeerResponding(DeadPeerDetectorEntry entry, DateTime timeoutTimestampUtc)
        {
            _bus.Send(new MarkPeerAsRespondingCommand(entry.Descriptor.PeerId, timeoutTimestampUtc)).Wait(_commandTimeout);
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
            if (_detectionThread != null && !_detectionThread.Join(2000))
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
                    OnError(ex);
                }
            }

            _logger.Info("MainLoop stopped");
        }

        private void OnError(Exception ex)
        {
            _logger.ErrorFormat("MainLoop error: {0}", ex);

            try
            {
                Error?.Invoke(ex);
            }
            catch (Exception errorException)
            {
                _logger.Error(errorException);
            }
        }
    }
}
