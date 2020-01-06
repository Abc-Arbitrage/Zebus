using System.Collections.Generic;

namespace Abc.Zebus
{
    public interface ISnapshot<TMessage> where TMessage : IEvent
    {
        List<TMessage> Content { get; }
    }
}
