using System;
using System.Threading.Tasks;
using Abc.Zebus.Directory.Configuration;
using Abc.Zebus.Util;
using log4net;

namespace Abc.Zebus.Directory.DeadPeerDetection
{
    public class DeadPeerDetectorEntry
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(DeadPeerDetectorEntry));
        private readonly IDirectoryConfiguration _configuration;
        private readonly IBus _bus;
        private readonly TaskScheduler _taskScheduler;
        private DateTime? _oldestUnansweredPingTimeUtc;
        private DateTime? _timeoutTimestampUtc;

        public DeadPeerDetectorEntry(PeerDescriptor descriptor, IDirectoryConfiguration configuration, IBus bus, TaskScheduler taskScheduler)
        {
            Descriptor = descriptor;
            _configuration = configuration;
            _bus = bus;
            _taskScheduler = taskScheduler;
        }

        public event Action<DeadPeerDetectorEntry, DateTime> PeerTimeoutDetected = delegate { };
        public event Action<DeadPeerDetectorEntry, DateTime> PeerRespondingDetected = delegate { };
        public event Action<DeadPeerDetectorEntry, DateTime> PingMissed = delegate { };

        public PeerDescriptor Descriptor { get; set; }
        public DeadPeerStatus Status { get; private set; }

        public bool IsUp
        {
            get { return Descriptor.Peer.IsUp; }
        }

        public bool WasRestarted
        {
            get
            {
                lock (this)
                {
                    return Descriptor.TimestampUtc > _timeoutTimestampUtc;
                }
            }
        }

        public void Process(DateTime timestampUtc, bool shouldSendPing)
        {
            if (WasRestarted)
                Reset();

            var hasReachedTimeout = Status == DeadPeerStatus.Up && HasReachedTimeout();
            if (hasReachedTimeout)
                Timeout();
            else if (shouldSendPing)
                Ping(timestampUtc);
        }

        public void Ping(DateTime timestampUtc)
        {
            lock (this)
            {
                if (_oldestUnansweredPingTimeUtc == null)
                    _oldestUnansweredPingTimeUtc = timestampUtc;
                var elapsedTimeSinceFirstPing = SystemDateTime.UtcNow - _oldestUnansweredPingTimeUtc;
                if (elapsedTimeSinceFirstPing > _configuration.PeerPingInterval)
                    PingMissed(this, timestampUtc);
            }

            SendPingCommand(timestampUtc);
        }

        public void Timeout()
        {
            DateTime timeoutTimestampUtc;

            lock (this)
            {
                if (_oldestUnansweredPingTimeUtc == null)
                    return;

                timeoutTimestampUtc = _oldestUnansweredPingTimeUtc.Value;
                _timeoutTimestampUtc = timeoutTimestampUtc;
                Status = DeadPeerStatus.Down;
            }

            var descriptor = Descriptor;

            _logger.WarnFormat("Peer timeout, PeerId: {0}", descriptor.PeerId);

            PeerTimeoutDetected(this, timeoutTimestampUtc);
        }

        public void Reset()
        {
            bool wasDown;

            lock (this)
            {
                _oldestUnansweredPingTimeUtc = null;
                _timeoutTimestampUtc = null;

                wasDown = Status == DeadPeerStatus.Down;
                Status = DeadPeerStatus.Up;
            }

            if (wasDown)
                _logger.InfoFormat("Peer reset, PeerId: {0}", Descriptor.PeerId);
        }

        public bool HasReachedTimeout()
        {
            lock (this)
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

        public void OnPingCommandAck(Task<CommandResult> pingTask, object state)
        {
            if (pingTask.IsFaulted)
            {
                _logger.DebugFormat("Ping failed, PeerId: {0}, Exception: {1}", Descriptor.PeerId, pingTask.Exception);
                return;
            }
            if (!pingTask.Result.IsSuccess)
            {
                _logger.DebugFormat("Ping failed, PeerId: {0}", Descriptor.PeerId);
                return;
            }

            var pingTimestampUtc = (DateTime)state;

            var peer = Descriptor.Peer;
            if (!peer.IsResponding)
            {
                PeerRespondingDetected(this, pingTimestampUtc);

                peer.IsResponding = true;
            }

            Reset();
        }
    }
}