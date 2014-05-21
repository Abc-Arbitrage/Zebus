namespace Abc.Zebus.Dispatch
{
    public interface IDispatcherTaskSchedulerFactory
    {
        DispatcherTaskScheduler Create(string queueName);
    }
}