using System;
using System.Threading.Tasks;

namespace Abc.Zebus.Dispatch
{
    public interface IMessageHandlerInvoker
    {
        Type MessageHandlerType { get; }
        Type MessageType { get; }
        MessageTypeId MessageTypeId { get; }
        bool ShouldBeSubscribedOnStartup { get; }
        bool ShouldCreateStartedTasks { get; }
        string DispatchQueueName { get; }
        bool CanInvokeSynchronously { get; }
        int BatchSize { get; }

        void InvokeMessageHandler(IMessageHandlerInvocation invocation);
        Task InvokeMessageHandlerAsync(IMessageHandlerInvocation invocation);
        bool ShouldHandle(IMessage message);
    }
}