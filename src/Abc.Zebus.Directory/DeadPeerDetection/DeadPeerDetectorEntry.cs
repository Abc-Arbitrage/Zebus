﻿using System;
using System.Threading.Tasks;
using Abc.Zebus.Directory.Configuration;
using Abc.Zebus.Util;
using Microsoft.Extensions.Logging;

namespace Abc.Zebus.Directory.DeadPeerDetection
{
    public class DeadPeerDetectorEntry : IDisposable
    {
        private static readonly ILogger _logger = ZebusLogManager.GetLogger(typeof(DeadPeerDetectorEntry));
        private readonly IDirectoryConfiguration _configuration;
        private readonly IBus _bus;
        private readonly TaskScheduler _taskScheduler;
        private readonly object _lock = new object();
        private DateTime? _lastPingTimeUtc;
        private DateTime? _oldestUnansweredPingTimeUtc;
        private DateTime? _timeoutTimestampUtc;
        private bool _disposed;

        public DeadPeerDetectorEntry(PeerDescriptor descriptor, IDirectoryConfiguration configuration, IBus bus, TaskScheduler taskScheduler)
        {
            Descriptor = descriptor;
            _configuration = configuration;
            _bus = bus;
            _taskScheduler = taskScheduler;
        }

        public event Action<DeadPeerDetectorEntry, DateTime>? PeerTimeoutDetected;
        public event Action<DeadPeerDetectorEntry, DateTime>? PeerRespondingDetected;
        public event Action<DeadPeerDetectorEntry, DateTime>? PingTimeout;

        public PeerDescriptor Descriptor { get; set; }
        public DeadPeerStatus Status { get; private set; }

        public bool IsUp => Descriptor.Peer.IsUp;

        public bool WasRestarted
        {
            get
            {
                lock (_lock)
                {
                    return Descriptor.TimestampUtc > _timeoutTimestampUtc;
                }
            }
        }

        public void Process(DateTime timestampUtc)
        {
            if (WasRestarted)
                Reset();

            var hasReachedTimeout = Status == DeadPeerStatus.Up && HasReachedTimeout();
            if (hasReachedTimeout)
                Timeout();
            else
                PingIfRequired(timestampUtc);
        }

        public void PingIfRequired(DateTime timestampUtc)
        {
            if (ShouldSendPing(timestampUtc))
                Ping(timestampUtc);
        }

        private bool ShouldSendPing(DateTime timestampUtc)
        {
            if (_lastPingTimeUtc == null)
                return true;

            var elapsedSinceLastPing = timestampUtc - _lastPingTimeUtc.Value;
            return elapsedSinceLastPing >= _configuration.PeerPingInterval;
        }

        public void Ping(DateTime timestampUtc)
        {
            lock (_lock)
            {
                _lastPingTimeUtc = timestampUtc;
                if (_oldestUnansweredPingTimeUtc == null)
                    _oldestUnansweredPingTimeUtc = timestampUtc;
                var elapsedTimeSinceFirstPing = SystemDateTime.UtcNow - _oldestUnansweredPingTimeUtc;
                if (elapsedTimeSinceFirstPing > _configuration.PeerPingInterval)
                    PingTimeout?.Invoke(this, timestampUtc);
            }

            SendPingCommand(timestampUtc);
        }

        public void Timeout()
        {
            DateTime timeoutTimestampUtc;

            lock (_lock)
            {
                if (_oldestUnansweredPingTimeUtc == null)
                    return;

                timeoutTimestampUtc = _oldestUnansweredPingTimeUtc.Value;
                _timeoutTimestampUtc = timeoutTimestampUtc;
                Status = DeadPeerStatus.Down;
            }

            var descriptor = Descriptor;

            _logger.LogWarning($"Peer timeout, PeerId: {descriptor.PeerId}");

            PeerTimeoutDetected?.Invoke(this, timeoutTimestampUtc);
        }

        public void Reset()
        {
            bool wasDown;

            lock (_lock)
            {
                _oldestUnansweredPingTimeUtc = null;
                _timeoutTimestampUtc = null;

                wasDown = Status == DeadPeerStatus.Down;
                Status = DeadPeerStatus.Up;
            }

            if (wasDown)
                _logger.LogInformation($"Peer reset, PeerId: {Descriptor.PeerId}");
        }

        public bool HasReachedTimeout()
        {
            lock (_lock)
            {
                if (_oldestUnansweredPingTimeUtc == null)
                    return false;

                var elapsed = (SystemDateTime.UtcNow - _oldestUnansweredPingTimeUtc.Value).Duration();

                if (Descriptor.HasDebuggerAttached)
                    return elapsed >= _configuration.DebugPeerPingTimeout;

                if (Descriptor.IsPersistent)
                    return elapsed >= _configuration.PersistentPeerPingTimeout;

                return elapsed >= _configuration.TransientPeerPingTimeout;
            }
        }

        private void SendPingCommand(DateTime timestampUtc)
        {
            _bus.Send(new PingPeerCommand(), Descriptor.Peer).ContinueWith(OnPingCommandAck, timestampUtc, _taskScheduler);
        }

        public void OnPingCommandAck(Task<CommandResult> pingTask, object? state)
        {
            if (pingTask.IsFaulted)
            {
                _logger.LogDebug(pingTask.Exception, $"Ping failed, PeerId: {Descriptor.PeerId}");
                return;
            }

            if (!pingTask.Result.IsSuccess)
            {
                _logger.LogDebug($"Ping failed, PeerId: {Descriptor.PeerId}");
                return;
            }

            var timestampUtc = default(DateTime);
            var peerRespondingDetected = false;

            lock (_lock)
            {
                if (_disposed)
                {
                    _logger.LogWarning($"Ping ignored on disposed entry, PeerId: {Descriptor.PeerId}");
                    return;
                }

                timestampUtc = Descriptor.TimestampUtc + TimeSpan.FromMilliseconds(1) ?? SystemDateTime.UtcNow;
                var peer = Descriptor.Peer;
                if (!peer.IsResponding)
                {
                    peerRespondingDetected = true;
                    peer.IsResponding = true;
                }
            }

            if (peerRespondingDetected)
                PeerRespondingDetected?.Invoke(this, timestampUtc);

            Reset();
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _disposed = true;
            }
        }
    }
}
