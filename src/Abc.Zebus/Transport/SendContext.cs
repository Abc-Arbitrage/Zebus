using System.Collections.Generic;

namespace Abc.Zebus.Transport
{
    public class SendContext
    {
        public List<PeerId> PersistentPeerIds { get; } = new List<PeerId>();
        public Peer PersistencePeer { get; set; }

        public bool WasPersisted(PeerId peerId)
        {
            for (var index = 0; index < PersistentPeerIds.Count; index++)
            {
                if (PersistentPeerIds[index] == peerId)
                    return true;
            }

            return false;
        }
    }
}
