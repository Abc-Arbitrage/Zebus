using System.Collections.Generic;

namespace Abc.Zebus.Dispatch.Pipes;

public interface IPipeManager
{
    void EnablePipe(string pipeName);
    void DisablePipe(string pipeName);

    PipeInvocation BuildPipeInvocation(IMessageHandlerInvoker messageHandlerInvoker, List<IMessage> messages, MessageContext messageContext);
}
