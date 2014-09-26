using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Directory;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Lotus;
using Abc.Zebus.Serialization;
using Abc.Zebus.Transport;
using Abc.Zebus.Util;
using Abc.Zebus.Util.Extensions;
using log4net;
using Newtonsoft.Json;

namespace Abc.Zebus.Core
{
    public class Bus : IBus, IMessageDispatchFactory
    {
        private const string _deserializationFailureDumpsDirectoryName = "deserialization_failure_dumps";
        private readonly UniqueTimestampProvider _deserializationFailureTimestampProvider = new UniqueTimestampProvider();
        private readonly ConcurrentDictionary<MessageId, TaskCompletionSource<CommandResult>> _messageIdToTaskCompletionSources = new ConcurrentDictionary<MessageId, TaskCompletionSource<CommandResult>>();
        private CustomThreadPoolTaskScheduler _completionResultTaskScheduler;
        private readonly Dictionary<Subscription, int> _subscriptions = new Dictionary<Subscription, int>();
        private readonly BusMessageLogger _messageLogger = BusMessageLogger.Get<Bus>();
        private readonly ILog _logger = LogManager.GetLogger(typeof(Bus));
        private readonly ITransport _transport;
        private readonly IPeerDirectory _directory;
        private readonly IMessageSerializer _serializer;
        private readonly IMessageDispatcher _messageDispatcher;
        private readonly IStoppingStrategy _stoppingStrategy;
        private PeerId _peerId;
        private bool _isRunning;
        
        public event Action Starting = delegate { };
        public event Action Started = delegate { };
        public event Action Stopping = delegate { };
        public event Action Stopped = delegate { };
 
        public Bus(ITransport transport, IPeerDirectory directory, IMessageSerializer serializer, IMessageDispatcher messageDispatcher, IStoppingStrategy stoppingStrategy)
        {
            _transport = transport;
            _transport.MessageReceived += OnTransportMessageReceived;
            _directory = directory;
            _directory.PeerUpdated += OnPeerUpdated;
            _serializer = serializer;
            _messageDispatcher = messageDispatcher;
            _stoppingStrategy = stoppingStrategy;
        }

        public PeerId PeerId
        {
            get { return _peerId; }
        }

        public bool IsRunning
        {
            get { return _isRunning; }
        }

        public string EndPoint
        {
            get { return _transport.InboundEndPoint; }
        }

        public void Configure(PeerId peerId, string environment)
        {
            _peerId = peerId;
            _transport.Configure(peerId, environment);
        }

        public virtual void Start()
        {
            Starting();

            _completionResultTaskScheduler = new CustomThreadPoolTaskScheduler(4);
            _logger.DebugFormat("Loading invokers...");
            _messageDispatcher.LoadMessageHandlerInvokers();

            PerformAutoSubscribe();

            _logger.DebugFormat("Starting message dispatcher...");
            _messageDispatcher.Start();

            _logger.DebugFormat("Starting transport...");
            _transport.Start();

            _logger.DebugFormat("Registering on directory...");
            var self = new Peer(PeerId, EndPoint);
            _directory.Register(this, self, GetSubscriptions());

            _transport.OnRegistered();

            _isRunning = true;

            Started();
        }

        private void PerformAutoSubscribe()
        {
            _logger.DebugFormat("Performing auto subscribe...");

            var autoSubscribeInvokers = _messageDispatcher.GetMessageHanlerInvokers().Where(x => x.ShouldBeSubscribedOnStartup);
            foreach (var invoker in autoSubscribeInvokers)
            {
                var subscription = new Subscription(invoker.MessageTypeId);
                _subscriptions[subscription] = 1 + _subscriptions.GetValueOrDefault(subscription);
            }
        }

        protected virtual IEnumerable<Subscription> GetSubscriptions()
        {
            lock (_subscriptions)
            {
                return _subscriptions.Keys.ToList();
            }
        }

        public virtual void Stop()
        {
            if (!_isRunning)
                return;
            
            Stopping();

            _directory.Unregister(this);

            _isRunning = false;

            _stoppingStrategy.Stop(_transport, _messageDispatcher);

            _subscriptions.Clear();
            _messageIdToTaskCompletionSources.Clear();
            _completionResultTaskScheduler.Dispose();

            Stopped();
        }

        public void Publish(IEvent message)
        {
            var peersHandlingMessage = _directory.GetPeersHandlingMessage(message).ToList();

            var localDispatchEnabled = LocalDispatch.Enabled;
            var shouldBeHandledLocally = localDispatchEnabled && peersHandlingMessage.Any(x => x.Id == _peerId);
            if (shouldBeHandledLocally)
                HandleLocalMessage(message, null);

            var targetPeers = localDispatchEnabled ? peersHandlingMessage.Where(x => x.Id != _peerId).ToList() : peersHandlingMessage;
            SendTransportMessage(null, message, targetPeers, true, shouldBeHandledLocally);
        }

        public Task<CommandResult> Send(ICommand message)
        {
            var peers = _directory.GetPeersHandlingMessage(message);
            if (peers.Count == 0)
                throw new InvalidOperationException("Unable to find peer for specified command, " + BusMessageLogger.ToString(message) + ". Did you change the namespace?");

            var self = peers.FirstOrDefault(x => x.Id == _peerId);

            if (self != null)
                return Send(message, self);

            if (peers.Count > 1)
            {
                var exceptionMessage = string.Format("{0} peers are handling {1}. Peers: {2}.", peers.Count, BusMessageLogger.ToString(message), string.Join(", ", peers.Select(p => p.ToString())));
                throw new InvalidOperationException(exceptionMessage);
            }

            return Send(message, peers[0]);
        }

        public Task<CommandResult> Send(ICommand message, Peer peer)
        {
            if (peer == null)
                throw new ArgumentNullException("peer");

            var taskCompletionSource = new TaskCompletionSource<CommandResult>();

            if (LocalDispatch.Enabled && peer.Id == _peerId)
            {
                HandleLocalMessage(message, taskCompletionSource);
            }
            else
            {
                if (!peer.IsResponding && !message.TypeId().IsPersistent())
                {
                    var exceptionMessage = string.Format("Unable to send this transient message {0} while peer {1} is not responding.", BusMessageLogger.ToString(message), peer.Id);
                    throw new InvalidOperationException(exceptionMessage);
                }
                var messageId = MessageId.NextId();
                _messageIdToTaskCompletionSources.TryAdd(messageId, taskCompletionSource);
                SendTransportMessage(messageId, message, peer, true);
            }

            return taskCompletionSource.Task;
        }

        public IDisposable Subscribe(Subscription[] subscriptions, SubscriptionOptions options = SubscriptionOptions.Default)
        {
            var shouldHaveHanlderInvoker = (options & SubscriptionOptions.ThereIsNoHandlerButIKnowWhatIAmDoing) == 0;
            if (shouldHaveHanlderInvoker)
                EnsureMessageHandlerInvokerExists(subscriptions);

            AddSubscriptions(subscriptions);

            return new DisposableAction(() => RemoveSubscriptions(subscriptions));
        }

        public IDisposable Subscribe(Subscription subscription, SubscriptionOptions options = SubscriptionOptions.Default)
        {
            var subscriptions = new[] { subscription };

            var shouldHaveHanlderInvoker = (options & SubscriptionOptions.ThereIsNoHandlerButIKnowWhatIAmDoing) == 0;
            if (shouldHaveHanlderInvoker)
                EnsureMessageHandlerInvokerExists(subscriptions);
            
            AddSubscriptions(subscriptions);

            return new DisposableAction(() => RemoveSubscriptions(subscriptions));
        }

        public IDisposable Subscribe<T>(Action<T> handler) where T : class, IMessage
        {
            var eventHandlerInvoker = new EventHandlerInvoker<T>(handler);
            var subscription = new Subscription(eventHandlerInvoker.MessageTypeId);

            _messageDispatcher.AddInvoker(eventHandlerInvoker);

            AddSubscriptions(new[] { subscription });

            return new DisposableAction(() =>
            {
                RemoveSubscriptions(new[] { subscription });
                _messageDispatcher.RemoveInvoker(eventHandlerInvoker);
            });
        }

        public IDisposable Subscribe(Subscription[] subscriptions, Action<IMessage> handler)
        {
            var eventHandlerInvokers = subscriptions.Select(x => new EventHandlerInvoker(handler, x.MessageTypeId.GetMessageType())).ToList();

            foreach (var eventHandlerInvoker in eventHandlerInvokers)
            {
                _messageDispatcher.AddInvoker(eventHandlerInvoker);
            }

            AddSubscriptions(subscriptions);

            return new DisposableAction(() =>
            {
                RemoveSubscriptions(subscriptions);
                foreach (var eventHandlerInvoker in eventHandlerInvokers)
                {
                    _messageDispatcher.RemoveInvoker(eventHandlerInvoker);
                }
            });
        }

        private void EnsureMessageHandlerInvokerExists(Subscription[] subscriptions)
        {
            foreach (var subscription in subscriptions)
            {
                if (!_messageDispatcher.GetMessageHanlerInvokers().Any(x => x.MessageTypeId == subscription.MessageTypeId))
                    throw new ArgumentException(string.Format("No handler available for message type Id: {0}", subscription.MessageTypeId));
            }
        }

        private void AddSubscriptions(IEnumerable<Subscription> subscriptions)
        {
            lock (_subscriptions)
            {
                foreach (var subscription in subscriptions)
                {
                    _subscriptions[subscription] = 1 + _subscriptions.GetValueOrDefault(subscription);
                }
            }
            OnSubscriptionsUpdated();
        }

        private void RemoveSubscriptions(IEnumerable<Subscription> subscriptions)
        {
            lock (_subscriptions)
            {
                foreach (var subscription in subscriptions)
                {
                    var subscriptionCount = _subscriptions.GetValueOrDefault(subscription);
                    if (subscriptionCount <= 1)
                        _subscriptions.Remove(subscription);
                    else
                        _subscriptions[subscription] = subscriptionCount - 1;
                }
            }
            OnSubscriptionsUpdated();
        }

        protected void OnSubscriptionsUpdated()
        {
            _directory.Update(this, GetSubscriptions());
        }

        public void Reply(int errorCode)
        {
            var messageContext = MessageContext.Current;
            if (messageContext == null)
                throw new InvalidOperationException("Reply called without message context");

            messageContext.ReplyCode = errorCode;
        }

        public void Reply(IMessage response)
        {
            var messageContext = MessageContext.Current;
            if (messageContext == null)
                throw new InvalidOperationException("Reply called without message context");

            messageContext.ReplyResponse = response;
        }

        private void OnPeerUpdated(PeerId peerId, PeerUpdateAction peerUpdateAction)
        {
            _transport.OnPeerUpdated(peerId, peerUpdateAction);
        }

        private void OnTransportMessageReceived(TransportMessage transportMessage)
        {
            if (!transportMessage.MessageTypeId.IsInfrastructure())
                HandleRemoteMessage(transportMessage);
            else if (transportMessage.MessageTypeId == MessageExecutionCompleted.TypeId)
                HandleMessageExecutionCompleted(transportMessage);
            else
                HandleRemoteMessage(transportMessage, true);
        }

        public MessageDispatch CreateMessageDispatch(TransportMessage transportMessage)
        {
            return CreateMessageDispatch(transportMessage, synchronousDispatch: false, sendAcknowledgment: false);
        }

        private MessageDispatch CreateMessageDispatch(TransportMessage transportMessage, bool synchronousDispatch, bool sendAcknowledgment = true)
        {
            var message = ToMessage(transportMessage);
            if (message == null)
                return null;

            var context = MessageContext.CreateNew(transportMessage);
            var continuation = sendAcknowledgment ? GetOnRemoteMessageDispatchedContinuation(transportMessage) : ((dispatch, result) => {}) ;
            return new MessageDispatch(context, message, continuation, synchronousDispatch);
        }

        protected virtual void HandleRemoteMessage(TransportMessage transportMessage, bool synchronous = false)
        {
            var dispatch = CreateMessageDispatch(transportMessage, synchronous);
            if (dispatch == null)
                return;

            _messageLogger.InfoFormat("RECV remote: {0} from {3} ({2} bytes). [{1}]", dispatch.Message, transportMessage.Id, transportMessage.MessageBytes.Length, transportMessage.Originator.SenderId);
            _messageDispatcher.Dispatch(dispatch);
        }

        private Action<MessageDispatch, DispatchResult> GetOnRemoteMessageDispatchedContinuation(TransportMessage transportMessage)
        {
            return (dispatch, dispatchResult) =>
            {
                SendMessageProcessingFailedIfNeeded(dispatch, dispatchResult, transportMessage);

                if (dispatch.Message is ICommand)
                {
                    var messageExecutionCompleted = MessageExecutionCompleted.Create(dispatch.Context, dispatchResult, _serializer);
                    var shouldLogMessageExecutionCompleted = _messageLogger.IsInfoEnabled(dispatch.Message);
                    SendTransportMessage(null, messageExecutionCompleted, dispatch.Context.GetSender(), shouldLogMessageExecutionCompleted);
                }

                AckTransportMessage(transportMessage);
            };
        }

        private void SendMessageProcessingFailedIfNeeded(MessageDispatch dispatch, DispatchResult dispatchResult, TransportMessage failingTransportMessage = null)
        {
            if (dispatchResult.Errors.Count == 0 || dispatchResult.Errors.All(error => error is DomainException))
                return;

            if (failingTransportMessage == null)
                failingTransportMessage = ToTransportMessage(dispatch.Message, MessageId.NextId());

            string jsonMessage;
            try
            {
                jsonMessage = JsonConvert.SerializeObject(dispatch.Message);
            }
            catch (Exception ex)
            {
                jsonMessage = string.Format("Unable to serialize message :{0}{1}", Environment.NewLine, ex);
            }
            var errorMessages = dispatchResult.Errors.Select(error => error.ToString());
            var errorMessage = string.Join(Environment.NewLine + Environment.NewLine, errorMessages);
            var messageProcessingFailed = new MessageProcessingFailed(failingTransportMessage, jsonMessage, errorMessage, SystemDateTime.UtcNow, dispatchResult.ErrorHandlerTypes.Select(x => x.FullName).ToArray());
            var peers = _directory.GetPeersHandlingMessage(messageProcessingFailed);
            SendTransportMessage(ToTransportMessage(messageProcessingFailed, MessageId.NextId()), peers);
        }

        private void HandleMessageExecutionCompleted(TransportMessage transportMessage)
        {
            var message = (MessageExecutionCompleted)ToMessage(transportMessage);
            if (message == null)
                return;

            HandleMessageExecutionCompleted(transportMessage, message);
        }

        protected virtual void HandleMessageExecutionCompleted(TransportMessage transportMessage, MessageExecutionCompleted message)
        {
            _messageLogger.DebugFormat("RECV : {0}", message);
            TaskCompletionSource<CommandResult> taskCompletionSource;
            if (!_messageIdToTaskCompletionSources.TryRemove(message.SourceCommandId, out taskCompletionSource))
                return;

            var response = message.PayloadTypeId != null ? ToMessage(message.PayloadTypeId, message.Payload, transportMessage.Originator) : null;
            var commandResult = new CommandResult(message.ErrorCode, response);

            var task = new Task(() => taskCompletionSource.SetResult(commandResult));
            task.Start(_completionResultTaskScheduler);
        }

        protected virtual void HandleLocalMessage(IMessage message, TaskCompletionSource<CommandResult> taskCompletionSource)
        {
            _messageLogger.InfoFormat("RECV local: {0}", message);

            var context = MessageContext.CreateOverride(PeerId, EndPoint);
            var dispatch = new MessageDispatch(context, message, GetOnLocalMessageDispatchedContinuation(taskCompletionSource));

            _messageDispatcher.Dispatch(dispatch);
        }

        private Action<MessageDispatch, DispatchResult> GetOnLocalMessageDispatchedContinuation(TaskCompletionSource<CommandResult> taskCompletionSource)
        {
            return (dispatch, dispatchResult) =>
            {
                SendMessageProcessingFailedIfNeeded(dispatch, dispatchResult);

                if (taskCompletionSource == null)
                    return;

                var commandResult = new CommandResult(dispatch.Context.ReplyCode, dispatch.Context.ReplyResponse);
                taskCompletionSource.SetResult(commandResult);
            };
        }

        private void SendTransportMessage(MessageId? messageId, IMessage message, Peer peer, bool logEnabled)
        {
            SendTransportMessage(messageId, message, new[] { peer }, logEnabled);
        }

        private void SendTransportMessage(MessageId? messageId, IMessage message, IList<Peer> peers, bool logEnabled, bool locallyHandled = false)
        {
            if (peers.Count == 0)
            {
                if (!locallyHandled && logEnabled)
                    _messageLogger.InfoFormat("SEND: {0} with no target peer", message);

                return;
            }

            var transportMessage = ToTransportMessage(message, messageId ?? MessageId.NextId());

            if (logEnabled)
                _messageLogger.InfoFormat("SEND: {0} to {3} ({2} bytes) [{1}]", message, transportMessage.Id, transportMessage.MessageBytes.Length, peers);

            SendTransportMessage(transportMessage, peers);
        }

        protected void SendTransportMessage(TransportMessage transportMessage, IList<Peer> peers)
        {
            _transport.Send(transportMessage, peers);
        }

        protected void AckTransportMessage(TransportMessage transportMessage)
        {
            _transport.AckMessage(transportMessage);
        }

        protected TransportMessage ToTransportMessage(IMessage message, MessageId messageId)
        {
            return _serializer.ToTransportMessage(message, messageId, PeerId, EndPoint);
        }

        private IMessage ToMessage(TransportMessage transportMessage)
        {
            return ToMessage(transportMessage.MessageTypeId, transportMessage.MessageBytes, transportMessage.Originator);
        }

        private IMessage ToMessage(MessageTypeId messageTypeId, byte[] messageBytes, OriginatorInfo originator)
        {
            try
            {
                return _serializer.Deserialize(messageTypeId, messageBytes);
            }
            catch (Exception exception)
            {
                var dumpLocation = DumpMessageOnDisk(messageTypeId, messageBytes);
                var errorMessage = string.Format("Unable to deserialize message {0}. Originator: {1}. Message dumped at: {2}\r\n{3}", messageTypeId.FullName, originator.SenderId, dumpLocation, exception);
                _logger.Error(errorMessage);

                var processingFailed = new CustomProcessingFailed(GetType().FullName, errorMessage, SystemDateTime.UtcNow);
                Publish(processingFailed);
            }
            return null;
        }

        private string DumpMessageOnDisk(MessageTypeId messageTypeId, byte[] messageBytes)
        {
            try
            {
                var dumpDirectory = PathUtil.InBaseDirectory(_deserializationFailureDumpsDirectoryName);
                if (!System.IO.Directory.Exists(dumpDirectory))
                    System.IO.Directory.CreateDirectory(dumpDirectory);

                var dumpFileName = string.Format("{0:yyyyMMdd-HH-mm-ss.fffffff}_{1}", _deserializationFailureTimestampProvider.NextUtcTimestamp(), messageTypeId.FullName);
                var dumpFilePath = Path.Combine(dumpDirectory, dumpFileName);
                File.WriteAllBytes(dumpFilePath, messageBytes);
                return dumpFilePath;
            }
            catch (Exception ex)
            {
                return "Message could not be dumped: " + ex;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
