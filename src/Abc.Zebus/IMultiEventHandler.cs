using System;
using System.Collections.Generic;

namespace Abc.Zebus
{
    public interface IMultiEventHandler
    {
        void Handle(IEvent e);
        IEnumerable<Type> GetHandledEventTypes();
    }
}