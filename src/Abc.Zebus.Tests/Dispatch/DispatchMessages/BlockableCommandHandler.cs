using Abc.Zebus.Dispatch;

namespace Abc.Zebus.Tests.Dispatch.DispatchMessages
{
    public class BlockableCommandHandler : IMessageHandler<BlockableCommand>
    {
        public void Handle(BlockableCommand message)
        {
            message.Handle();
        }
    }
}