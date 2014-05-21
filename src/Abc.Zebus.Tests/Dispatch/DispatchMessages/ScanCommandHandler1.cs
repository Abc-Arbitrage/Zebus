namespace Abc.Zebus.Tests.Dispatch.DispatchMessages
{
    public class ScanCommandHandler1 : IMessageHandler<ScanCommand1>, IMessageHandler<ScanCommand2>, IMessageContextAware
    {
        public ScanCommand1 HandledCommand1;
        public ScanCommand2 HandledCommand2;
        public MessageContext CapturedContext;
        public MessageContext Context { get; set; }

        public void Handle(ScanCommand1 message)
        {
            HandledCommand1 = message;
            CapturedContext = MessageContext.Current;
        }

        public void Handle(ScanCommand2 message)
        {
            HandledCommand2 = message;
            CapturedContext = MessageContext.Current;
        }
    }
}