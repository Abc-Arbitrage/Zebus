using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Abc.Zebus.Persistence.Matching;
using Abc.Zebus.Persistence.Messages;
using Abc.Zebus.Persistence.Reporter;
using Abc.Zebus.Persistence.Storage;
using Abc.Zebus.Persistence.Util;
using Abc.Zebus.Serialization;
using Abc.Zebus.Transport;
using Abc.Zebus.Util;
using Microsoft.Extensions.Logging;

namespace Abc.Zebus.Persistence
{
    public class MessageReplayer : IMessageReplayer
    {
        private static readonly ILogger _logger = ZebusLogManager.GetLogger(typeof(MessageReplayer));
        private readonly BlockingCollection<TransportMessage> _liveMessages = new BlockingCollection<TransportMessage>();
        private readonly ConcurrentSet<MessageId> _unackedIds = new ConcurrentSet<MessageId>();
        private readonly IPersistenceConfiguration _persistenceConfiguration;
        private readonly IStorage _storage;
        private readonly IBus _bus;
        private readonly ITransport _transport;
        private readonly IInMemoryMessageMatcher _inMemoryMessageMatcher;
        private readonly Peer _self;
        private readonly Peer _peer;
        private readonly Guid _replayId;
        private readonly IPersistenceReporter _reporter;
        private readonly IMessageSerializer _messageSerializer;
        private CancellationTokenSource? _cancellationTokenSource;
        private Thread? _runThread;
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly int _replayBatchSize;
        private readonly SendContext _emptySendContext = new SendContext();

        public MessageReplayer(IPersistenceConfiguration persistenceConfiguration,
                               IStorage storage,
                               IBus bus,
                               ITransport transport,
                               IInMemoryMessageMatcher inMemoryMessageMatcher,
                               Peer peer,
                               Guid replayId,
                               IPersistenceReporter reporter,
                               IMessageSerializer messageSerializer)
        {
            _persistenceConfiguration = persistenceConfiguration;
            _storage = storage;
            _bus = bus;
            _transport = transport;
            _inMemoryMessageMatcher = inMemoryMessageMatcher;
            _self = new Peer(transport.PeerId, transport.InboundEndPoint);
            _peer = peer;
            _replayId = replayId;
            _reporter = reporter;
            _messageSerializer = messageSerializer;
            _replayBatchSize = _persistenceConfiguration.ReplayBatchSize;

            UnackedMessageCountThatReleasesNextBatch = _persistenceConfiguration.ReplayUnackedMessageCountThatReleasesNextBatch;
        }

        public int UnackedMessageCountThatReleasesNextBatch { get; set; }

        public event Action? Stopped;

        public void AddLiveMessage(TransportMessage message)
        {
            _liveMessages.Add(message);
        }

        public void Start()
        {
            var waitHandle = new ManualResetEvent(false);
            _inMemoryMessageMatcher.EnqueueWaitHandle(waitHandle);

            _cancellationTokenSource = new CancellationTokenSource();
            _runThread = BackgroundThread.Start(RunProc, waitHandle);
        }

        public bool Cancel()
        {
            _cancellationTokenSource?.Cancel();

            if (WaitForCompletion(5.Seconds()))
                return true;

            _logger.LogWarning($"Unable to cancel replayer, PeerId: {_peer.Id}");
            return false;
        }

        public void OnMessageAcked(MessageId messageId)
        {
            _unackedIds.Remove(messageId);
        }

        public bool WaitForCompletion(TimeSpan timeout)
        {
            return _runThread?.Join(timeout) ?? true;
        }

        private void RunProc(ManualResetEvent signal)
        {
            _logger.LogInformation($"Replay started, PeerId: {_peer.Id}");

            signal.WaitOne();
            signal.Dispose();

            _logger.LogInformation($"BatchPersister flushed, PeerId: {_peer.Id}");

            _stopwatch.Start();
            try
            {
                Run(_cancellationTokenSource!.Token);
                if (_cancellationTokenSource!.IsCancellationRequested)
                    _logger.LogWarning($"Replay cancelled, PeerId: {_peer.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Replay failed, PeerId: {_peer.Id}");
            }

            _stopwatch.Stop();

            _logger.LogInformation($"Replay stopped, PeerId: {_peer.Id}. It ran for {_stopwatch.Elapsed}");

            Stopped?.Invoke();
        }

        public void Run(CancellationToken cancellationToken)
        {
            _bus.Publish(new ReplaySessionStarted(_peer.Id, _replayId));

            var replayDuration = MeasureDuration();
            var totalReplayedCount = ReplayUnackedMessages(cancellationToken);
            _logger.LogInformation($"Replay phase ended for {_peer.Id}. {totalReplayedCount} messages replayed in {replayDuration.Value} ({totalReplayedCount / replayDuration.Value.TotalSeconds} msg/s)");

            if (cancellationToken.IsCancellationRequested)
                return;

            _transport.Send(ToTransportMessage(new ReplayPhaseEnded(_replayId)), new[] { _peer }, _emptySendContext);

            var safetyDuration = MeasureDuration();
            ForwardLiveMessages(cancellationToken);
            _logger.LogInformation($"Safety phase ended for {_peer.Id} ({safetyDuration.Value})");
            if (cancellationToken.IsCancellationRequested)
                return;

            _transport.Send(ToTransportMessage(new SafetyPhaseEnded(_replayId)), new[] { _peer }, _emptySendContext);
            _bus.Publish(new ReplaySessionEnded(_peer.Id, _replayId));
        }

        private int ReplayUnackedMessages(CancellationToken cancellationToken)
        {
            using var reader = _storage.CreateMessageReader(_peer.Id);
            if (reader == null)
                return 0;
            var totalMessageCount = 0;

            foreach (var partition in reader.GetUnackedMessages().TakeWhile(m => !cancellationToken.IsCancellationRequested).Partition(_replayBatchSize, true))
            {
                var messageSentCount = 0;
                var batchDuration = MeasureDuration();
                var readAndSendDuration = MeasureDuration();
                foreach (var message in partition.Select(DeserializeTransportMessage))
                {
                    _unackedIds.Add(message.Id);
                    ReplayMessage(message);
                    messageSentCount++;
                }

                totalMessageCount += messageSentCount;

                _logger.LogInformation($"Read and send for last batch of {messageSentCount} msgs for {_peer.Id} took {readAndSendDuration.Value}. ({messageSentCount / readAndSendDuration.Value.TotalSeconds} msg/s)");
                WaitForAcks(cancellationToken);
                _logger.LogInformation($"Last batch for {_peer.Id} took {batchDuration.Value} to be totally replayed ({messageSentCount / batchDuration.Value.TotalSeconds} msg/s)");
                _reporter.AddReplaySpeedReport(new ReplaySpeedReport(messageSentCount, readAndSendDuration.Value, batchDuration.Value));
            }

            _logger.LogInformation($"Replay finished for peer {_peer.Id}. Disposing the reader");
            return totalMessageCount;
        }

        private static TransportMessage DeserializeTransportMessage(byte[] row) => TransportMessage.Deserialize(row);

        private void WaitForAcks(CancellationToken cancellationToken)
        {
            if (_unackedIds.Count <= UnackedMessageCountThatReleasesNextBatch)
                return;

            var expectedAckCount = Math.Max(0, _unackedIds.Count - UnackedMessageCountThatReleasesNextBatch);
            _logger.LogInformation($"Waiting for {expectedAckCount} ack(s) before proceeding to next batch for {_peer.Id}");

            var waitDuration = MeasureDuration();
            while (_unackedIds.Count > UnackedMessageCountThatReleasesNextBatch)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                Thread.Sleep(100);
            }

            _logger.LogInformation($"Batch acked in {waitDuration.Value} for peer {_peer.Id} ({expectedAckCount / waitDuration.Value.TotalSeconds} msg/s)");
            _logger.LogInformation($"Proceeding with next batch for {_peer.Id}");
        }

        private void ReplayMessage(TransportMessage unackedMessage)
        {
            var messageReplayed = new MessageReplayed(_replayId, unackedMessage);
            var transportMessage = ToTransportMessage(messageReplayed);

            _transport.Send(transportMessage, new[] { _peer }, _emptySendContext);
        }

        private void ForwardLiveMessages(CancellationToken cancellationToken)
        {
            var phaseEnd = DateTime.UtcNow + _persistenceConfiguration.SafetyPhaseDuration;

            while (DateTime.UtcNow < phaseEnd && !cancellationToken.IsCancellationRequested)
            {
                if (!_liveMessages.TryTake(out var liveMessage, 200))
                    continue;

                var messageReplayed = new MessageReplayed(_replayId, liveMessage);
                _transport.Send(ToTransportMessage(messageReplayed), new[] { _peer }, new SendContext());
            }
        }

        private TransportMessage ToTransportMessage(IMessage message, bool wasPersisted = false)
        {
            return new TransportMessage(message.TypeId(), _messageSerializer.Serialize(message), _self) { WasPersisted = wasPersisted };
        }

        private Lazy<TimeSpan> MeasureDuration()
        {
            var beginning = _stopwatch.Elapsed;
            return new Lazy<TimeSpan>(() => _stopwatch.Elapsed - beginning);
        }
    }
}
