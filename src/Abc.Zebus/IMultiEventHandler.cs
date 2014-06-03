using System;
using System.Collections.Generic;

namespace Abc.Zebus
{
    // TODO: remove in future versions
    public interface IMultiEventHandler
    {
        void Handle(IEvent e);
        IEnumerable<Type> GetHandledEventTypes();
    }
}