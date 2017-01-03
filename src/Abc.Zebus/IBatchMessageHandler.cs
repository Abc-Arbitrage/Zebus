using System.Collections.Generic;

namespace Abc.Zebus
{
    public interface IBatchMessageHandler<T> : IMessageHandler where T : class, IEvent
    {
        void Handle(List<T> messages);
    }
}