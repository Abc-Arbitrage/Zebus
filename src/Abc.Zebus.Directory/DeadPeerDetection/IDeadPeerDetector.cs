using System;

namespace Abc.Zebus.Directory.DeadPeerDetection
{
    public interface IDeadPeerDetector : IDisposable
    {
        event Action<Exception> Error;
        event Action PersistenceDownDetected;
        event Action<PeerId, DateTime> PingTimeout;

        void Start();
        void Stop();
    }
}
