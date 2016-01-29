namespace Abc.Zebus.Transport
{
    public struct PeerWithPersistenceInfo
    {
        public Peer Peer;
        public bool WasPersisted;

        public PeerWithPersistenceInfo(Peer peer, bool wasPersisted)
        {
            Peer = peer;
            WasPersisted = wasPersisted;
        }
    }
}