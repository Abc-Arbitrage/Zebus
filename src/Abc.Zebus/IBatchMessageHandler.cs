using System.Collections.Generic;

namespace Abc.Zebus
{
    public interface IBatchMessageHandler<T> : IMessageHandler where T : class
    {
        void Handle(List<T> messages);
    }
}