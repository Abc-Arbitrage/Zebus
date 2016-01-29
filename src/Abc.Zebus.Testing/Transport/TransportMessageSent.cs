using System.Collections.Generic;
using Abc.Zebus.Transport;

namespace Abc.Zebus.Testing.Transport
{
    public class TransportMessageSent
    {
        public readonly TransportMessage TransportMessage;
        public readonly IEnumerable<PeerWithPersistenceInfo> Targets;

        public TransportMessageSent(TransportMessage transportMessage, params PeerWithPersistenceInfo[] targets)
        {
            TransportMessage = transportMessage;
            Targets = targets;
        }
    }
}