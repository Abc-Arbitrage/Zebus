using JetBrains.Annotations;

namespace Abc.Zebus
{
    [UsedImplicitly]
    public interface ICustomInvokerMessageHandler<T> : IMessageHandler<T>
        where T : class, IMessage
    {
    }
}
