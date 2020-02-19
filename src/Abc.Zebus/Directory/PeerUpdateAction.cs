namespace Abc.Zebus.Directory
{
    public enum PeerUpdateAction
    {
        Stopped,
        Started,
        /// <summary>  Peer subscriptions are updated </summary>
        Updated,
        Decommissioned,
    }
}
