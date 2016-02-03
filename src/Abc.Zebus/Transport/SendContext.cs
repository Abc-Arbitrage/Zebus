using System.Collections.Generic;

namespace Abc.Zebus.Transport
{
    public class SendContext
    {
        public readonly List<PeerId> PersistedPeerIds = new List<PeerId>();
    }
}