namespace Abc.Zebus.Persistence.Storage
{
    public readonly struct NonAckedCount
    {
        public readonly PeerId PeerId;
        public readonly int Count;

        public NonAckedCount(PeerId peerId, int count)
        {
            PeerId = peerId;
            Count = count;
        } 
    }
}