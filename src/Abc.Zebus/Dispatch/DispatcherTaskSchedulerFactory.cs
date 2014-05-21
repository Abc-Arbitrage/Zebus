namespace Abc.Zebus.Dispatch
{
    public class DispatcherTaskSchedulerFactory : IDispatcherTaskSchedulerFactory
    {
        public DispatcherTaskScheduler Create(string queueName)
        {
            return new DispatcherTaskScheduler(queueName + ".DispatchThread");
        }
    }
}