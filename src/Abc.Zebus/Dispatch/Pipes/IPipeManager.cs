namespace Abc.Zebus.Dispatch.Pipes
{
    public interface IPipeManager
    {
        void EnablePipe(string pipeName);
        void DisablePipe(string pipeName);

        PipeInvocation BuildPipeInvocation(IMessageHandlerInvoker messageHandlerInvoker, IMessage message, MessageContext messageContext);
    }
}