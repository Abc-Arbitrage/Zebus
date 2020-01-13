using Abc.Zebus.Dispatch.Pipes;
using Abc.Zebus.Util;

namespace Abc.Zebus.Dispatch
{
    public class DispatchQueueFactory : IDispatchQueueFactory
    {
        private readonly IPipeManager _pipeManager;
        private readonly IBusConfiguration _configuration;

        public DispatchQueueFactory(IPipeManager pipeManager, IBusConfiguration configuration)
        {
            _pipeManager = pipeManager;
            _configuration = configuration;
        }

        public DispatchQueue Create(string queueName)
        {
            return new DispatchQueue(_pipeManager, _configuration.MessagesBatchSize, queueName);
        }
    }
}
