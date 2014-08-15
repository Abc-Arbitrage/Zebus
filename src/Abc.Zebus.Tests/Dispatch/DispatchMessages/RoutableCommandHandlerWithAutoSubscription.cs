namespace Abc.Zebus.Tests.Dispatch.DispatchMessages
{
    [SubscriptionMode(SubscriptionMode.Auto)]
    public class RoutableCommandHandlerWithAutoSubscription : IMessageHandler<RoutableCommand>
    {
        public void Handle(RoutableCommand message)
        {
        }
    }
}