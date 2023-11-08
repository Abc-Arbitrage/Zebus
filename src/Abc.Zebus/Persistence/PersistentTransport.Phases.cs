using System;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Transport;
using Microsoft.Extensions.Logging;

namespace Abc.Zebus.Persistence;

public partial class PersistentTransport
{
    private abstract class Phase
    {
        private readonly ManualResetEvent _endOfProcessingSignal = new(false);

        protected Phase(PersistentTransport transport)
        {
            Transport = transport;
        }

        protected PersistentTransport Transport { get; set; }

        public virtual void OnStart()
        {
        }

        public virtual void OnRegistered()
        {
        }

        public virtual void OnReplayEvent(IReplayEvent replayEvent)
        {
            switch (replayEvent)
            {
                case MessageReplayed messageReplayed:
                    // the message was persisted because it comes from the persistence
                    // but previous Zebus versions do not specify the WasPersisted field
                    // => force WasPersisted to support previous Zebus version and make sure the message will be acked
                    messageReplayed.Message.WasPersisted = true;

                    OnMessageReplayed(messageReplayed);
                    return;

                case ReplayPhaseEnded:
                {
                    var safetyPhase = new SafetyPhase(Transport);
                    Transport.SetPhase(safetyPhase);
                    Transport.StartReceptionThread();
                    return;
                }

                case SafetyPhaseEnded:
                {
                    Transport._pendingReceives.CompleteAdding();
                    _endOfProcessingSignal.WaitOne();
                    var normalPhase = new NormalPhase(Transport);
                    Transport.SetPhase(normalPhase);
                    Transport._receivedMessagesIds.Clear();
                    break;
                }
            }
        }

        public virtual void ProcessPendingReceive(TransportMessage transportMessage)
        {
            _logger.LogError($"DISCARDING MESSAGE {transportMessage.MessageTypeId} {transportMessage.Id}");
        }

        protected virtual void OnMessageReplayed(MessageReplayed messageReplayed)
        {
        }

        public void PendingReceivesProcessingCompleted()
        {
            _endOfProcessingSignal.Set();
        }

        public abstract void OnRealTimeMessage(TransportMessage transportMessage);
    }

    private class ReplayPhase : Phase
    {
        private readonly ManualResetEventSlim _replayEventReceivedSignal = new();
        private int _replayCount;

        public ReplayPhase(PersistentTransport transport)
            : base(transport)
        {
        }

        public override void OnReplayEvent(IReplayEvent replayEvent)
        {
            if (!_replayEventReceivedSignal.IsSet)
                _replayEventReceivedSignal.Set();

            if (replayEvent is ReplayPhaseEnded)
                _logger.LogInformation($"Replayed {_replayCount} messages");

            base.OnReplayEvent(replayEvent);
        }

        public override void OnRegistered()
        {
            Transport._currentReplayId = Guid.NewGuid();

            StartReplayEventTimeoutDetector();

            var startMessageReplayCommand = new StartMessageReplayCommand(Transport._currentReplayId.Value);
            Transport.EnqueueOrSendToPersistenceService(startMessageReplayCommand);
        }

        private async void StartReplayEventTimeoutDetector()
        {
            await Task.Run(() =>
            {
                var configuration = Transport._configuration;

                if (!_replayEventReceivedSignal.Wait(configuration.StartReplayTimeout))
                    throw new PersistenceUnreachableException(configuration.StartReplayTimeout, configuration.DirectoryServiceEndPoints);
            });
        }

        protected override void OnMessageReplayed(MessageReplayed messageReplayed)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug($"REPLAY: {messageReplayed.Message.MessageTypeId} {messageReplayed.Message.Id}");

            Transport.TriggerMessageReceived(messageReplayed.Message);
            Transport._receivedMessagesIds.TryAdd(messageReplayed.Message.Id, true);

            _replayCount++;
            if (_replayCount % 100 == 0)
                _logger.LogInformation($"Replayed {_replayCount} messages...");
        }

        public override void OnRealTimeMessage(TransportMessage transportMessage)
        {
            Transport._pendingReceives.Add(transportMessage);
        }
    }

    private class SafetyPhase : Phase
    {
        public SafetyPhase(PersistentTransport transport)
            : base(transport)
        {
        }

        public override void ProcessPendingReceive(TransportMessage transportMessage)
        {
            if (Transport._receivedMessagesIds.ContainsKey(transportMessage.Id))
                return;

            try
            {
                Transport.TriggerMessageReceived(transportMessage);
            }
            catch (Exception exception)
            {
                var errorMessage = $"Unable to handle message {transportMessage.MessageTypeId.FullName} during SafetyPhase.";
                _logger.LogError(exception, errorMessage);
            }

            Transport._receivedMessagesIds.TryAdd(transportMessage.Id, true);
        }

        protected override void OnMessageReplayed(MessageReplayed messageReplayed)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug($"FORWARD: {messageReplayed.Message.MessageTypeId} {messageReplayed.Message.Id}");

            Transport._pendingReceives.Add(messageReplayed.Message);
        }

        public override void OnRealTimeMessage(TransportMessage transportMessage)
        {
            Transport._pendingReceives.Add(transportMessage);
        }
    }

    private class NormalPhase : Phase
    {
        public NormalPhase(PersistentTransport transport)
            : base(transport)
        {
        }

        // TODO: throw exception if receiving a replay message?

        public override void OnRealTimeMessage(TransportMessage transportMessage)
        {
            Transport.TriggerMessageReceived(transportMessage);
        }
    }

    private class NoReplayPhase : Phase
    {
        public NoReplayPhase(PersistentTransport transport)
            : base(transport)
        {
        }

        public override void OnRealTimeMessage(TransportMessage transportMessage)
        {
            Transport.TriggerMessageReceived(transportMessage);
        }
    }
}
