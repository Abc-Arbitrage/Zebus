using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Abc.Zebus
{
    [UsedImplicitly]
    public interface IAsyncMessageHandler { }
    public interface IAsyncMessageHandler<T> : IAsyncMessageHandler where T : class
    {
        Task Handle(T message);
    }
}