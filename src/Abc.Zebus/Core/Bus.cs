using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Directory;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Lotus;
using Abc.Zebus.Monitoring;
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
        private readonly ConcurrentDictionary<MessageId, TaskCompletionSource<CommandResult>> _messageIdToTaskCompletionSources = new ConcurrentDictionary<MessageId, TaskCompletionSource<CommandResult>>();
        private CustomThreadPoolTaskScheduler _completionResultTaskScheduler;
        private readonly HashSet<Subscription> _subscriptions = new HashSet<Subscription>();
        private readonly BusMessageLogger _messageLogger = new BusMessageLogger();
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
            _transport.SocketConnected += OnSocketConnected;
            _transport.SocketDisconnected += OnSocketDisconnected;
            _directory = directory;
            _directory.PeerUpdated += OnPeerUpdated;
            _serializer = serializer;
            _messageDispatcher = messageDispatcher;
            _stoppingStrategy = stoppingStrategy;
        }

        private void OnSocketDisconnected(PeerId remotePeerId, string remoteEndpoint)
        {
            if(_isRunning) // When stopping the bus, we don't want to reestablish the connection with the services handling this event
                Publish(new SocketDisconnected(_peerId, remotePeerId, remoteEndpoint));
        }

        private void OnSocketConnected(PeerId remotePeerId, string remoteEndpoint)
        {
            Publish(new SocketConnected(_peerId, remotePeerId, remoteEndpoint));
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
                _subscriptions.Add(new Subscription(invoker.MessageTypeId));
            }
        }

        protected virtual IEnumerable<Subscription> GetSubscriptions()
        {
            lock (_subscriptions)
            {
                return _subscriptions.ToList();
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
            if (localDispatchEnabled && peersHandlingMessage.Any(x => x.Id == _peerId))
                HandleLocalMessage(message, null);

            var targetPeers = localDispatchEnabled ? peersHandlingMessage.Where(x => x.Id != _peerId).ToList() : peersHandlingMessage;
            SendTransportMessage(null, message, targetPeers);
        }

        public Task<CommandResult> Send(ICommand message)
        {
            var peers = _directory.GetPeersHandlingMessage(message);
            if (peers.Count == 0)
                throw new InvalidOperationException("Unable to find peer for specified command, " + _messageLogger.ToString(message) + ". Did you change the namespace?");

            var self = peers.FirstOrDefault(x => x.Id == _peerId);

            if (self != null)
                return Send(message, self);

            if (peers.Count > 1)
            {
                var exceptionMessage = string.Format("{0} peers are handling {1}. Peers: {2}.", peers.Count, _messageLogger.ToString(message), string.Join(", ", peers.Select(p => p.ToString())));
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
                var messageId = MessageId.NextId();
                _messageIdToTaskCompletionSources.TryAdd(messageId, taskCompletionSource);
                SendTransportMessage(messageId, message, peer);
            }

            return taskCompletionSource.Task;
        }

        public IDisposable Subscribe(Subscription[] subscriptions, SubscriptionOptions options = SubscriptionOptions.Default)
        {
            var shouldHaveHanlderInvoker = (options & SubscriptionOptions.ThereIsNoHandlerButIKnowWhatIAmDoing) == 0;
            if (shouldHaveHanlderInvoker)
            {
                foreach (var subscription in subscriptions)
                {
                    if (_messageDispatcher.GetMessageHanlerInvokers().All(x => x.MessageTypeId != subscription.MessageTypeId))            
                        throw new ArgumentException(string.Format("No handler available for message type Id: {0}", subscription.MessageTypeId));
                }
            }

            lock (_subscriptions)
            {
                _subscriptions.AddRange(subscriptions);
            }

            OnSubscriptionsUpdated();

            return new DisposableAction(() => Unsubscribe(subscriptions));
        }

        public IDisposable Subscribe(Subscription subscription, SubscriptionOptions options = SubscriptionOptions.Default)
        {
            var shouldHaveHanlderInvoker = (options & SubscriptionOptions.ThereIsNoHandlerButIKnowWhatIAmDoing) == 0;
            if (shouldHaveHanlderInvoker && _messageDispatcher.GetMessageHanlerInvokers().All(x => x.MessageTypeId != subscription.MessageTypeId))
                throw new ArgumentException(string.Format("No handler available for message type Id: {0}", subscription.MessageTypeId));

            bool updated;
            lock (_subscriptions)
            {
                updated = _subscriptions.Add(subscription);
            }

            if (updated)
                OnSubscriptionsUpdated();

            return new DisposableAction(() => Unsubscribe(subscription));
        }

        public IDisposable Subscribe<T>(Action<T> handler) where T : class, IMessage
        {
            var eventHandlerInvoker = new EventHandlerInvoker<T>(handler);
            var subscription = new Subscription(eventHandlerInvoker.MessageTypeId);

            return AddSubscriptionWithInvoker(eventHandlerInvoker, subscription);
        }

        private IDisposable AddSubscriptionWithInvoker(IMessageHandlerInvoker invoker, Subscription subscription)
        {
            _messageDispatcher.AddInvoker(invoker);

            lock (_subscriptions)
            {
                _subscriptions.Add(subscription);
            }
            OnSubscriptionsUpdated();

            return new DisposableAction(() =>
            {
                Unsubscribe(subscription);
                _messageDispatcher.RemoveInvoker(invoker);
            });
        }


        public IDisposable Subscribe(Type messageType, IMultiEventHandler multiEventHandler) 
        {
            var eventHandlerInvoker = new MultiEventHandlerInvoker(messageType, multiEventHandler);
            var subscription = new Subscription(eventHandlerInvoker.MessageTypeId);

            return AddSubscriptionWithInvoker(eventHandlerInvoker, subscription);

        }

        private void Unsubscribe(Subscription subscription)
        {
            bool updated;

            lock (_subscriptions)
            {
                updated = _subscriptions.Remove(subscription);
            }

            if (updated)
                OnSubscriptionsUpdated();
        }

        private void Unsubscribe(IEnumerable<Subscription> subscriptions)
        {
            lock (_subscriptions)
            {
                _subscriptions.RemoveRange(subscriptions);
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

            _messageLogger.LogFormat("HANDLE remote: {0} from {3} ({2} bytes). [{1}]", dispatch.Message, transportMessage.Id, transportMessage.MessageBytes.Length, transportMessage.Originator.SenderId);
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
                    SendTransportMessage(null, messageExecutionCompleted, dispatch.Context.GetSender());
                }

                AckTransportMessage(transportMessage);
            };
        }

        private void SendMessageProcessingFailedIfNeeded(MessageDispatch dispatch, DispatchResult dispatchResult, TransportMessage failingTransportMessage = null)
        {
            if (dispatchResult.Errors.All(error => error is DomainException))
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
            _messageLogger.LogFormat("HANDLE local: {0}", message);

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

        private void SendTransportMessage(MessageId? messageId, IMessage message, Peer peer)
        {
            SendTransportMessage(messageId, message, new[] { peer });
        }

        private void SendTransportMessage(MessageId? messageId, IMessage message, IList<Peer> peers)
        {
            if (peers.Count == 0)
            {
                _messageLogger.LogFormat("SEND: {0} with no target peer", message);
                return;
            }

            var transportMessage = ToTransportMessage(message, messageId ?? MessageId.NextId());
            _messageLogger.LogFormat("SEND: {0} to {3} ({2} bytes) [{1}]", message, transportMessage.Id, transportMessage.MessageBytes.Length, peers);

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
                var errorMessage = string.Format("Unable to deserialize message {0}. Originator: {1}", messageTypeId.FullName, originator.SenderId);
                _logger.Error(errorMessage, exception);

                var processingFailed = new CustomProcessingFailed(GetType().FullName, exception.Message, SystemDateTime.UtcNow);
                Publish(processingFailed);
            }
            return null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
