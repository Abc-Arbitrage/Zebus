namespace Abc.Zebus.Testing.Dispatch
{
    public class TestMessageHandlerInvoker<TMessage> : TestMessageHandlerInvoker where TMessage : class, IMessage
    {
        public TestMessageHandlerInvoker(bool shouldBeSubscribedOnStartup = true) : base(typeof(Handler), typeof(TMessage), shouldBeSubscribedOnStartup)
        {
        }

        public class Handler : IMessageHandler<TMessage>
        {
            public void Handle(TMessage message)
            {
            }
        }
    }
}