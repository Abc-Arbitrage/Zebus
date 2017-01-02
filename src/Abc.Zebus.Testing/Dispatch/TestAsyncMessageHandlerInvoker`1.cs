using System.Threading.Tasks;

namespace Abc.Zebus.Testing.Dispatch
{
    public class TestAsyncMessageHandlerInvoker<TMessage> : TestAsyncMessageHandlerInvoker where TMessage : class, IMessage
    {
        public TestAsyncMessageHandlerInvoker(bool shouldBeSubscribedOnStartup = true) : base(typeof(Handler), typeof(TMessage), shouldBeSubscribedOnStartup)
        {
        }

        public class Handler : IAsyncMessageHandler<TMessage>
        {
            public Task Handle(TMessage message)
            {
                return Task.FromResult(0);
            }
        }
    }
}