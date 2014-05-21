using System;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Transport;
using log4net;

namespace Abc.Zebus.Persistence
{
    public partial class PersistentTransport
    {
        private abstract class Phase
        {
            private readonly ManualResetEvent _endOfProcessingSignal = new ManualResetEvent(false);

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
                var messageReplayed = replayEvent as MessageReplayed;
                if (messageReplayed != null)
                {
                    OnMessageReplayed(messageReplayed);
                    return;
                }

                var replayPhaseEnded = replayEvent as ReplayPhaseEnded;
                if (replayPhaseEnded != null)
                {
                    var safetyPhase = new SafetyPhase(Transport);
                    Transport.SetPhase(safetyPhase);
                    Transport.StartReceptionThread();
                    return;
                }

                var safetyPhaseEnded = replayEvent as SafetyPhaseEnded;
                if (safetyPhaseEnded != null)
                {
                    Transport._pendingReceives.CompleteAdding();
                    _endOfProcessingSignal.WaitOne();
                    var normalPhase = new NormalPhase(Transport);
                    Transport.SetPhase(normalPhase);
                    Transport._receivedMessagesIds.Clear();
                    return;
                }
            }

            public virtual void ProcessPendingReceive(TransportMessage transportMessage)
            {
                Transport._logger.ErrorFormat("DISCARDING MESSAGE {0} {1}", transportMessage.MessageTypeId, transportMessage.Id);
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
            private readonly ManualResetEventSlim _replayEventReceivedSignal = new ManualResetEventSlim();
            private int _replayCount;


            public ReplayPhase(PersistentTransport transport)
                : base(transport)
            {
            }

            public override void OnReplayEvent(IReplayEvent replayEvent)
            {
                if (!_replayEventReceivedSignal.IsSet)
                    _replayEventReceivedSignal.Set();

                if(replayEvent is ReplayPhaseEnded)
                    Transport._logger.InfoFormat("Replayed {0} messages", _replayCount);

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
                await Task.Factory.StartNew(() =>
                {
                    var configuration = Transport._configuration;

                    if (!_replayEventReceivedSignal.Wait(configuration.StartReplayTimeout))
                        throw new PersistenceUnreachableException(configuration.StartReplayTimeout, configuration.DirectoryServiceEndPoints);
                });
            }

            protected override void OnMessageReplayed(MessageReplayed messageReplayed)
            {
                Transport._logger.DebugFormat("REPLAY: {0} {1}", messageReplayed.Message.MessageTypeId, messageReplayed.Message.Id);

                messageReplayed.Message.ForcePersistenceAck = true;
                Transport.TriggerMessageReceived(messageReplayed.Message);
                Transport._receivedMessagesIds.TryAdd(messageReplayed.Message.Id, true);

                _replayCount++;
                if (_replayCount % 100 == 0)
                    Transport._logger.InfoFormat("Replayed {0} messages...", _replayCount);

            }

            public override void OnRealTimeMessage(TransportMessage transportMessage)
            {
                Transport._pendingReceives.Add(transportMessage);
            }
        }

        private class SafetyPhase : Phase
        {
            private readonly ILog _logger = LogManager.GetLogger(typeof(SafetyPhase));

            public SafetyPhase(PersistentTransport transport) : base(transport)
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
                    var errorMessage = string.Format("Unable to handle message {0} during SafetyPhase.", transportMessage.MessageTypeId.FullName);
                    _logger.Error(errorMessage, exception);
                }
                Transport._receivedMessagesIds.TryAdd(transportMessage.Id, true);
            }

            protected override void OnMessageReplayed(MessageReplayed messageReplayed)
            {
                Transport._logger.DebugFormat("FORWARD: {0} {1}", messageReplayed.Message.MessageTypeId, messageReplayed.Message.Id);

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
}