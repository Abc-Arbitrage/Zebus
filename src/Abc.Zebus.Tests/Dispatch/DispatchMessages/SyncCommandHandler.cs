namespace Abc.Zebus.Tests.Dispatch.DispatchMessages
{
    public class SyncCommandHandler : IMessageHandler<DispatchCommand>
    {
        public bool Called;

        public void Handle(DispatchCommand message)
        {
            Called = true;
        }
    }
}