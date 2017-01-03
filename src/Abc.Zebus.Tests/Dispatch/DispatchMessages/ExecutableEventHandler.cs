namespace Abc.Zebus.Tests.Dispatch.DispatchMessages
{
    public class ExecutableEventHandler : IMessageHandler<ExecutableEvent>
    {
        public void Handle(ExecutableEvent message)
        {
            message.Execute(null);
        }
    }
}