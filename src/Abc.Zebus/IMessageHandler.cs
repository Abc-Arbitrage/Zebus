using Abc.Zebus.Util.Annotations;

namespace Abc.Zebus
{
    [UsedImplicitly]
    public interface IMessageHandler {}
    public interface IMessageHandler<T> : IMessageHandler where T : class
    {
        void Handle(T message);
    }
}