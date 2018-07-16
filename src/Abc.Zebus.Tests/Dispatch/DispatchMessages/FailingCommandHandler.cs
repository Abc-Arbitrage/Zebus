namespace Abc.Zebus.Tests.Dispatch.DispatchMessages
{
    public class FailingCommandHandler : IMessageHandler<FailingCommand>
    {
        public void Handle(FailingCommand message)
        {
            throw message.Exception;
        }
    }
}