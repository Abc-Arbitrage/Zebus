using Abc.Zebus.Dispatch;

namespace Abc.Zebus.Tests.Dispatch.DispatchMessages.Namespace1
{
    public class UseOtherQueue : IProvideDispatchQueueNameForCurrentNamespace
    {
        public string QueueName => "OtherQueue";
    }
}