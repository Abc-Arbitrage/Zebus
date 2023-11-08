using System;
using System.Collections.Generic;

namespace Abc.Zebus.Dispatch.Pipes;

public interface IPipeSource
{
    IEnumerable<IPipe> GetPipes(Type messageHandlerType);
}
