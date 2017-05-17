using System.Diagnostics.CodeAnalysis;

namespace Abc.Zebus
{
    [SuppressMessage("ReSharper", "TypeParameterCanBeVariant")]
    public interface IMessageHandler<T> : IMessageHandler
        where T : class, IMessage
    {
        void Handle(T message);
    }
}
