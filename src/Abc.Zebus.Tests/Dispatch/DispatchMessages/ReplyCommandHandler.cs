namespace Abc.Zebus.Tests.Dispatch.DispatchMessages
{
    public class ReplyCommandHandler : IMessageHandler<ReplyCommand>, IMessageContextAware
    {
        public MessageContext Context { get; set; }

        public void Handle(ReplyCommand message)
        {
            Context.ReplyCode = ReplyCommand.ReplyCode;
        }
    }
}