using System;
using System.Diagnostics;
using System.Threading;
using Abc.Zebus.Hosting;
using Abc.Zebus.Util;
using log4net;

namespace Abc.Zebus.Persistence.Initialization
{
    public class MessageReplayerInitializer : HostInitializer
    {
        private static readonly ILog _logger = LogManager.GetLogger(typeof(MessageReplayerInitializer));
        private readonly IMessageReplayerRepository _messageReplayerRepository;
        private readonly TimeSpan _waitTimeout;

        public MessageReplayerInitializer(IPersistenceConfiguration configuration, IMessageReplayerRepository messageReplayerRepository)
        {
            _messageReplayerRepository = messageReplayerRepository;
            _waitTimeout = configuration.SafetyPhaseDuration + 30.Seconds();
        }

        public override void BeforeStop()
        {
            base.BeforeStop();

            _messageReplayerRepository.DeactivateMessageReplayers();

            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < _waitTimeout && _messageReplayerRepository.HasActiveMessageReplayers())
                Thread.Sleep(200);

            if (_messageReplayerRepository.HasActiveMessageReplayers())
                _logger.WarnFormat("Stopping with active message replayers");
        }
    }
}
