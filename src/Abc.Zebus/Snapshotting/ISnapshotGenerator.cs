namespace Abc.Zebus.Snapshotting
{
    public interface ISnapshotGenerator{}

    public interface ISnapshotGenerator<TMessage> : ISnapshotGenerator
        where TMessage : IEvent
    {
        ISnapshot<TMessage> GenerateSnapshot(Subscription subscription);
    }
}
