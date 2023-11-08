using Abc.Zebus.Transport;

namespace Abc.Zebus.Persistence;

public interface IPersistentTransport : ITransport
{
    int PendingPersistenceSendCount { get; }
}
