using Abc.Zebus.Dispatch.Pipes;

namespace Abc.Zebus.Dispatch
{
    public class DispatchQueueFactory : IDispatchQueueFactory
    {
        private readonly IPipeManager _pipeManager;

        public DispatchQueueFactory(IPipeManager pipeManager)
        {
            _pipeManager = pipeManager;
        }

        public DispatchQueue Create(string queueName)
        {
            return new DispatchQueue(_pipeManager, queueName);
        }
    }
}