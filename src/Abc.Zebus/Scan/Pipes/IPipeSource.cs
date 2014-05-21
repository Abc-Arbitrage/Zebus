using System;
using System.Collections.Generic;

namespace Abc.Zebus.Scan.Pipes
{
    public interface IPipeSource
    {
        IEnumerable<IPipe> GetPipes(Type messageHandlerType);
    }
}