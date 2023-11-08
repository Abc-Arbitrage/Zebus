using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Abc.Zebus.Dispatch;

public interface IMessageHandlerInvoker
{
    Type MessageHandlerType { get; }
    Type MessageType { get; }
    MessageTypeId MessageTypeId { get; }
    string DispatchQueueName { get; }
    MessageHandlerInvokerMode Mode { get; }

    IEnumerable<Subscription> GetStartupSubscriptions();
    void InvokeMessageHandler(IMessageHandlerInvocation invocation);
    Task InvokeMessageHandlerAsync(IMessageHandlerInvocation invocation);
    bool ShouldHandle(IMessage message);
    bool CanMergeWith(IMessageHandlerInvoker other);
}
