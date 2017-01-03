namespace Abc.Zebus
{
    public interface IMessageHandler<T> : IMessageHandler where T : class
    {
        void Handle(T message);
    }
}