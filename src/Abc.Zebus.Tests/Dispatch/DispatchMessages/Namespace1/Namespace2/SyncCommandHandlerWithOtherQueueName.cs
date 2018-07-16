using Abc.Zebus.Dispatch;

namespace Abc.Zebus.Tests.Dispatch.DispatchMessages.Namespace1.Namespace2
{
    public class SyncCommandHandlerWithOtherQueueName : IMessageHandler<DispatchCommand>
    {
        public string DispatchQueueName { get; set; }

        public void Handle(DispatchCommand message)
        {
            DispatchQueueName = DispatchQueue.GetCurrentDispatchQueueName();
        }
    }
}