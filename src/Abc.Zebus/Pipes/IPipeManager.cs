using ABC.ServiceBus;
using Abc.Zebus.Dispatch;

namespace Abc.Zebus.Pipes
{
    public interface IPipeManager
    {
        void EnablePipe(string pipeName);
        void DisablePipe(string pipeName);

        PipeInvocation BuildPipeInvocation(IMessageHandlerInvoker messageHandlerInvoker, IMessage message, MessageContext messageContext);
    }
}