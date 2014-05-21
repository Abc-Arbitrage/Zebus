namespace Abc.Zebus.Tests.Dispatch.DispatchMessages.Namespace1.Namespace2
{
    public class SyncCommandHandlerWithOtherQueueName : IMessageHandler<DispatchCommand>, IMessageContextAware
    {
        public string DispatchQueueName { get; set; }
        public MessageContext Context { get; set; }

        public void Handle(DispatchCommand message)
        {
            DispatchQueueName = Context.DispatchQueueName;
        }
    }
}