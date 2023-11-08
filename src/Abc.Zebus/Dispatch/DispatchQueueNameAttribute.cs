using System;

namespace Abc.Zebus.Dispatch;

[AttributeUsage(AttributeTargets.Class)]
public class DispatchQueueNameAttribute : Attribute
{
    public DispatchQueueNameAttribute(string queueName)
    {
        QueueName = queueName;
    }

    public string QueueName { get; }
}
