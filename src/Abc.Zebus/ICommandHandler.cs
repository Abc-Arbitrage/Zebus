namespace Abc.Zebus
{
    public interface ICommandHandler
    {
    }

    public interface ICommandHandler<T> : ICommandHandler, IMessageHandler<T> where T : class, ICommand
    {
    }
}