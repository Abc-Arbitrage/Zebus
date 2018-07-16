using System.Collections.Generic;

namespace Abc.Zebus
{
    public interface IBatchedMessageHandler<T> : IMessageHandler
        where T : class, IEvent
    {
        void Handle(IList<T> messages);
    }
}
