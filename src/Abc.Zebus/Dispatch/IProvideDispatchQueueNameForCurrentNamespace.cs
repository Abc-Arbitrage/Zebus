namespace Abc.Zebus.Dispatch
{
    public interface IProvideDispatchQueueNameForCurrentNamespace
    {
        string QueueName { get; } 
    }
}