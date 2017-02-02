using System.Threading.Tasks;
using Abc.Zebus.Dispatch;

namespace Abc.Zebus.Testing.Dispatch
{
    public interface IAsyncExecutableMessage : IMessage
    {
        Task ExecuteAsync(IMessageHandlerInvocation invocation);
    }
}
