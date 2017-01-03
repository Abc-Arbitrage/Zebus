using Abc.Zebus.Dispatch;

namespace Abc.Zebus.Testing.Dispatch
{
    public interface IExecutableMessage : IMessage
    {
        void Execute(IMessageHandlerInvocation invocation);
    }
}