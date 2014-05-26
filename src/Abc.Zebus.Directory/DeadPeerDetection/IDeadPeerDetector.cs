using System;

namespace Abc.Zebus.Directory.DeadPeerDetection
{
    public interface IDeadPeerDetector
    {
        event Action PersistenceDownDetected;
        event Action<PeerId, DateTime> PeerDownDetected;
        event Action<PeerId, DateTime> PeerRespondingDetected;
        
        Action<Exception> ExceptionHandler { get; set; }
        
        void AfterStart();
        void DoPeriodicAction();
        void BeforeStop();
    }
}