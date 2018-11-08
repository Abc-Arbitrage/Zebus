namespace Abc.Zebus.Tests.Dispatch.DispatchMessages
{
    public class SyncCommandHandler : IMessageHandler<DispatchCommand>
    {
        public bool Called;
        public DispatchCommand ReceivedMessage;

        public void Handle(DispatchCommand message)
        {
            ReceivedMessage = message;
            Called = true;
        }
    }
}
