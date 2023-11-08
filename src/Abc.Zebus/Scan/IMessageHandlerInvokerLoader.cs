using System.Collections.Generic;
using Abc.Zebus.Dispatch;

namespace Abc.Zebus.Scan;

public interface IMessageHandlerInvokerLoader
{
    IEnumerable<IMessageHandlerInvoker> LoadMessageHandlerInvokers(TypeSource typeSource);
}
