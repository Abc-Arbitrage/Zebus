using Abc.Zebus.Dispatch;

namespace Abc.Zebus.Tests.Dispatch.DispatchMessages
{
    public class BlockableCommandHandler : IMessageHandler<BlockableCommand>
    {
        public void Handle(BlockableCommand message)
        {
            message.HandleStarted = true;
            message.DispatchQueueName = DispatchQueue.GetCurrentDispatchQueueName();

            message.Callback?.Invoke();

            if (message.IsBlocking)
                message.BlockingSignal.WaitOne();

            message.HandleStopped = true;
        }
    }
}