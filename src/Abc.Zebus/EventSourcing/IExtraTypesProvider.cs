using System;
using System.Collections.Generic;

namespace Abc.Zebus.EventSourcing
{
    public interface IExtraTypesProvider
    {
        IEnumerable<Type> GetTypes();
    }
}