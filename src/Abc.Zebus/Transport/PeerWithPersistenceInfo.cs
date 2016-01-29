namespace Abc.Zebus.Transport
{
    public struct PeerWithPersistenceInfo
    {
        public readonly Peer Peer;
        public readonly bool WasPersisted;

        public PeerWithPersistenceInfo(Peer peer, bool wasPersisted)
        {
            Peer = peer;
            WasPersisted = wasPersisted;
        }
    }
}