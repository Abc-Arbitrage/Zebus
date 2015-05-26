using System;

namespace Abc.Zebus.Directory.DeadPeerDetection
{
    public interface IDeadPeerDetector : IDisposable
    {
        event Action PersistenceDownDetected;
        event Action<PeerId, DateTime> PeerDownDetected;
        event Action<PeerId, DateTime> PeerRespondingDetected;
        event Action<PeerId, DateTime> PingMissed;
        
        Action<Exception> ExceptionHandler { get; set; }
        
        void Start();
        void Stop();
    }
}