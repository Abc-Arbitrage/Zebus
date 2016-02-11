using System;
using Abc.Zebus.Dispatch;

namespace Abc.Zebus.Tests.Dispatch.DispatchMessages
{
    [DispatchQueueName("DispatchQueue1")]
    public class ForwardCommandHandler : IMessageHandler<ForwardCommand>, IMessageContextAware
    {
        public Action<MessageContext> Action { get; set; }
        public MessageContext Context { get; set; }

        public void Handle(ForwardCommand message)
        {
            Action?.Invoke(Context);
        }
    }
}
