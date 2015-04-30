using Abc.Zebus.Routing;

namespace Abc.Zebus.Tests
{
    public static class TestDataBuilder
    {
         public static Subscription[] CreateSubscriptions<TMessage>() where TMessage : IMessage
         {
             return new[] { CreateSubscription<TMessage>() };
         }

         public static Subscription CreateSubscription<TMessage>(BindingKey? bindingKey = null) where TMessage : IMessage
         {
             return new Subscription(MessageUtil.TypeId<TMessage>(), bindingKey ?? BindingKey.Empty);
         }
    }
}