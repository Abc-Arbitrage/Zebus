using System;
using System.Collections.Generic;

namespace Abc.Zebus.Pipes
{
    public interface IPipeSource
    {
        IEnumerable<IPipe> GetPipes(Type messageHandlerType);
    }
}