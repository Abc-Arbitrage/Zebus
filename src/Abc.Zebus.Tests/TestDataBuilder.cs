namespace Abc.Zebus.Tests
{
    public static class TestDataBuilder
    {
         public static Subscription[] CreateSubscriptions<T1>() where T1 : IMessage
         {
             return new[] { new Subscription(MessageUtil.TypeId<T1>()) };
         }
    }
}