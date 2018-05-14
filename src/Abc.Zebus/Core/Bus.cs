using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Directory;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Lotus;
using Abc.Zebus.Persistence;
using Abc.Zebus.Routing;
using Abc.Zebus.Serialization;
using Abc.Zebus.Transport;
using Abc.Zebus.Util;
using Abc.Zebus.Util.Annotations;
using Abc.Zebus.Util.Extensions;
using log4net;
using Newtonsoft.Json;

namespace Abc.Zebus.Core
{
    public class Bus : IBus, IMessageDispatchFactory
    {
        private static readonly BusMessageLogger _messageLogger = new BusMessageLogger(typeof(Bus));
        private static readonly ILog _logger = LogManager.GetLogger(typeof(Bus));

        private readonly ConcurrentDictionary<MessageId, TaskCompletionSource<CommandResult>> _messageIdToTaskCompletionSources = new ConcurrentDictionary<MessageId, TaskCompletionSource<CommandResult>>();
        private readonly UniqueTimestampProvider _deserializationFailureTimestampProvider = new UniqueTimestampProvider();
        private readonly Dictionary<Subscription, int> _subscriptions = new Dictionary<Subscription, int>();
        private readonly ITransport _transport;
        private readonly IPeerDirectory _directory;
        private readonly IMessageSerializer _serializer;
        private readonly IMessageDispatcher _messageDispatcher;
        private readonly IMessageSendingStrategy _messageSendingStrategy;
        private readonly IStoppingStrategy _stoppingStrategy;
        private readonly IBindingKeyPredicateBuilder _predicateBuilder;
        private readonly IBusConfiguration _configuration;
        private Task _unsubscribeTask = Task.CompletedTask;
        private int _subscriptionsVersion;

        public Bus(ITransport transport, IPeerDirectory directory, IMessageSerializer serializer, IMessageDispatcher messageDispatcher, IMessageSendingStrategy messageSendingStrategy, IStoppingStrategy stoppingStrategy, IBindingKeyPredicateBuilder predicateBuilder, IBusConfiguration configuration)
        {
            _transport = transport;
            _transport.MessageReceived += OnTransportMessageReceived;
            _directory = directory;
            _directory.PeerUpdated += OnPeerUpdated;
            _serializer = serializer;
            _messageDispatcher = messageDispatcher;
            _messageSendingStrategy = messageSendingStrategy;
            _stoppingStrategy = stoppingStrategy;
            _predicateBuilder = predicateBuilder;
            _configuration = configuration;
        }

        public event Action Starting = delegate { };
        public event Action Started = delegate { };
        public event Action Stopping = delegate { };
        public event Action Stopped = delegate { };

        public PeerId PeerId { get; private set; }
        public string Environment { get; private set; }
        public bool IsRunning { get; private set; }
        public string EndPoint => _transport.InboundEndPoint;
        public string DeserializationFailureDumpDirectoryPath { get; set; } = PathUtil.InBaseDirectory("deserialization_failure_dumps");

        public void Configure(PeerId peerId, string environment)
        {
            PeerId = peerId;
            Environment = environment;
            _transport.Configure(peerId, environment);
        }

        public virtual void Start()
        {
            if (IsRunning)
                throw new InvalidOperationException("Unable to start, the bus is already running");

            Starting();

            var registered = false;
            try
            {
                _logger.DebugFormat("Loading invokers...");
                _messageDispatcher.LoadMessageHandlerInvokers();

                PerformAutoSubscribe();

                _logger.DebugFormat("Starting message dispatcher...");
                _messageDispatcher.Start();

                _logger.DebugFormat("Starting transport...");
                _transport.Start();

                IsRunning = true;

                _logger.DebugFormat("Registering on directory...");
                var self = new Peer(PeerId, EndPoint);
                _directory.RegisterAsync(this, self, GetSubscriptions()).Wait();
                registered = true;

                _transport.OnRegistered();
            }
            catch
            {
                InternalStop(registered);
                IsRunning = false;
                throw;
            }

            Started();
        }

        private void PerformAutoSubscribe()
        {
            _logger.DebugFormat("Performing auto subscribe...");

            var autoSubscribeInvokers = _messageDispatcher.GetMessageHanlerInvokers().Where(x => x.ShouldBeSubscribedOnStartup).ToList();

            lock (_subscriptions)
            {
                foreach (var invoker in autoSubscribeInvokers)
                {
                    var subscription = new Subscription(invoker.MessageTypeId);
                    _subscriptions[subscription] = 1 + _subscriptions.GetValueOrDefault(subscription);
                }
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
            if (!IsRunning)
                throw new InvalidOperationException("Unable to stop, the bus is not running");

            Stopping();

            InternalStop(true);

            Stopped();
        }

        private void InternalStop(bool unregister)
        {
            if (unregister)
                _directory.UnregisterAsync(this).Wait();

            _stoppingStrategy.Stop(_transport, _messageDispatcher);

            IsRunning = false;

            lock (_subscriptions)
            {
                _subscriptions.Clear();
                _unsubscribeTask = Task.CompletedTask;

                unchecked
                {
                    ++_subscriptionsVersion;
                }
            }

            _messageIdToTaskCompletionSources.Clear();
        }

        public void Publish(IEvent message)
        {
            if (!IsRunning)
                throw new InvalidOperationException("Unable to publish message, the bus is not running");

            var peersHandlingMessage = _directory.GetPeersHandlingMessage(message);

            var localDispatchEnabled = LocalDispatch.Enabled;
            var shouldBeHandledLocally = localDispatchEnabled && peersHandlingMessage.Any(x => x.Id == PeerId);
            if (shouldBeHandledLocally)
                HandleLocalMessage(message, null);

            var targetPeers = shouldBeHandledLocally ? peersHandlingMessage.Where(x => x.Id != PeerId).ToList() : peersHandlingMessage;
            SendTransportMessage(null, message, targetPeers, true, shouldBeHandledLocally);
        }

        public Task<CommandResult> Send(ICommand message)
        {
            if (!IsRunning)
                throw new InvalidOperationException("Unable to send message, the bus is not running");

            var peers = _directory.GetPeersHandlingMessage(message);
            if (peers.Count == 0)
                throw new InvalidOperationException("Unable to find peer for specified command, " + BusMessageLogger.ToString(message) + ". Did you change the namespace?");

            var self = peers.FirstOrDefault(x => x.Id == PeerId);

            if (self != null)
                return Send(message, self);

            if (peers.Count > 1)
            {
                var exceptionMessage = $"{peers.Count} peers are handling {BusMessageLogger.ToString(message)}. Peers: {string.Join(", ", peers.Select(p => p.ToString()))}.";
                throw new InvalidOperationException(exceptionMessage);
            }

            return Send(message, peers[0]);
        }

        public Task<CommandResult> Send(ICommand message, Peer peer)
        {
            if (peer == null)
                throw new ArgumentNullException(nameof(peer));

            if (!IsRunning)
                throw new InvalidOperationException("Unable to send message, the bus is not running");

            var taskCompletionSource = new TaskCompletionSource<CommandResult>();

            if (LocalDispatch.Enabled && peer.Id == PeerId)
            {
                HandleLocalMessage(message, taskCompletionSource);
            }
            else
            {
                var transportMessage = ToTransportMessage(message);

                if (!peer.IsResponding && !_messageSendingStrategy.IsMessagePersistent(transportMessage) && !message.TypeId().IsInfrastructure())
                    throw new InvalidOperationException($"Unable to send this transient message {BusMessageLogger.ToString(message)} while peer {peer.Id} is not responding.");

                _messageIdToTaskCompletionSources.TryAdd(transportMessage.Id, taskCompletionSource);

                var peers = new[] { peer };
                LogMessageSend(message, transportMessage, peers);
                SendTransportMessage(transportMessage, peers);
            }

            return taskCompletionSource.Task;
        }

        public async Task<IDisposable> SubscribeAsync([NotNull] SubscriptionRequest request)
        {
            ValidateSubscriptionRequest(request);

            if (!request.ThereIsNoHandlerButIKnowWhatIAmDoing)
                EnsureMessageHandlerInvokerExists(request.Subscriptions);

            request.MarkAsSubmitted();

            if (request.Batch != null)
                await request.Batch.WhenSubmittedAsync().ConfigureAwait(false);

            await AddSubscriptionsAsync(request).ConfigureAwait(false);

            return new DisposableAction(() => RemoveSubscriptions(request));
        }

        public async Task<IDisposable> SubscribeAsync([NotNull] SubscriptionRequest request, [NotNull] Action<IMessage> handler)
        {
            ValidateSubscriptionRequest(request);

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            request.MarkAsSubmitted();

            var eventHandlerInvokers = request.Subscriptions
                                              .GroupBy(x => x.MessageTypeId)
                                              .Select(x => new DynamicMessageHandlerInvoker(handler, x.Key.GetMessageType(), x.Select(s => s.BindingKey).ToList(), _predicateBuilder))
                                              .ToList();

            if (request.Batch != null)
                await request.Batch.WhenSubmittedAsync().ConfigureAwait(false);

            foreach (var eventHandlerInvoker in eventHandlerInvokers)
                _messageDispatcher.AddInvoker(eventHandlerInvoker);

            await AddSubscriptionsAsync(request).ConfigureAwait(false);

            return new DisposableAction(() =>
            {
                foreach (var eventHandlerInvoker in eventHandlerInvokers)
                    _messageDispatcher.RemoveInvoker(eventHandlerInvoker);

                RemoveSubscriptions(request);
            });
        }

        private void ValidateSubscriptionRequest(SubscriptionRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (!IsRunning)
                throw new InvalidOperationException("Cannot perform a subscription, the bus is not running");
        }

        private void EnsureMessageHandlerInvokerExists(IEnumerable<Subscription> subscriptions)
        {
            foreach (var subscription in subscriptions)
            {
                if (_messageDispatcher.GetMessageHanlerInvokers().All(x => x.MessageTypeId != subscription.MessageTypeId))
                    throw new ArgumentException($"No handler available for message type Id: {subscription.MessageTypeId}");
            }
        }

        private async Task AddSubscriptionsAsync(SubscriptionRequest request)
        {
            request.SubmissionSubscriptionsVersion = _subscriptionsVersion;

            if (request.Batch != null)
            {
                var batchSubscriptions = request.Batch.TryConsumeBatchSubscriptions();
                if (batchSubscriptions != null)
                {
                    try
                    {
                        await SendSubscriptionsAsync(batchSubscriptions).ConfigureAwait(false);
                        request.Batch.NotifyRegistrationCompleted(null);
                    }
                    catch (Exception ex)
                    {
                        request.Batch.NotifyRegistrationCompleted(ex);
                        throw;
                    }
                }
                else
                {
                    await request.Batch.WhenRegistrationCompletedAsync().ConfigureAwait(false);
                }
            }
            else
            {
                await SendSubscriptionsAsync(request.Subscriptions).ConfigureAwait(false);
            }

            async Task SendSubscriptionsAsync(IEnumerable<Subscription> subscriptions)
            {
                if (!IsRunning)
                    throw new InvalidOperationException("Cannot perform a subscription, the bus is not running");

                var updatedTypes = new HashSet<MessageTypeId>();

                lock (_subscriptions)
                {
                    foreach (var subscription in subscriptions)
                    {
                        updatedTypes.Add(subscription.MessageTypeId);
                        _subscriptions[subscription] = 1 + _subscriptions.GetValueOrDefault(subscription);
                    }
                }

                // Wait until all unsubscriptions are completed to prevent race conditions
                await WhenUnsubscribeCompletedAsync().ConfigureAwait(false);

                await UpdateDirectorySubscriptionsAsync(updatedTypes).ConfigureAwait(false);
            }
        }

        internal Task WhenUnsubscribeCompletedAsync()
            => _unsubscribeTask;

        private void RemoveSubscriptions(SubscriptionRequest request)
        {
            if (!IsRunning)
                return;

            var updatedTypes = new HashSet<MessageTypeId>();

            lock (_subscriptions)
            {
                if (request.SubmissionSubscriptionsVersion != _subscriptionsVersion)
                    return;

                foreach (var subscription in request.Subscriptions)
                {
                    updatedTypes.Add(subscription.MessageTypeId);

                    var subscriptionCount = _subscriptions.GetValueOrDefault(subscription);
                    if (subscriptionCount <= 1)
                        _subscriptions.Remove(subscription);
                    else
                        _subscriptions[subscription] = subscriptionCount - 1;
                }

                _unsubscribeTask = _unsubscribeTask.ContinueWith(async _ =>
                {
                    if (!IsRunning)
                        return;

                    lock (_subscriptions)
                    {
                        if (request.SubmissionSubscriptionsVersion != _subscriptionsVersion)
                            return;
                    }

                    await UpdateDirectorySubscriptionsAsync(updatedTypes);
                }).Unwrap();
            }
        }

        private async Task UpdateDirectorySubscriptionsAsync(HashSet<MessageTypeId> updatedTypes)
        {
            var subscriptions = GetSubscriptions().Where(sub => updatedTypes.Contains(sub.MessageTypeId));
            var subscriptionsByTypes = SubscriptionsForType.CreateDictionary(subscriptions);

            var subscriptionUpdates = new List<SubscriptionsForType>(updatedTypes.Count);
            foreach (var updatedMessageId in updatedTypes)
                subscriptionUpdates.Add(subscriptionsByTypes.GetValueOrDefault(updatedMessageId, new SubscriptionsForType(updatedMessageId)));

            await _directory.UpdateSubscriptionsAsync(this, subscriptionUpdates).ConfigureAwait(false);
        }

        public void Reply(int errorCode) => Reply(errorCode, null);

        public void Reply(int errorCode, string message)
        {
            var messageContext = MessageContext.Current;
            if (messageContext == null)
                throw new InvalidOperationException("Reply called without message context");

            messageContext.ReplyCode = errorCode;
            messageContext.ReplyMessage = message;
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
            if (transportMessage.MessageTypeId == MessageExecutionCompleted.TypeId)
            {
                HandleMessageExecutionCompleted(transportMessage);
            }
            else
            {
                var executeSynchronously = transportMessage.MessageTypeId.IsInfrastructure();
                HandleRemoteMessage(transportMessage, executeSynchronously);
            }
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
            var continuation = sendAcknowledgment ? GetOnRemoteMessageDispatchedContinuation(transportMessage) : ((dispatch, result) => { });
            return new MessageDispatch(context, message, continuation, synchronousDispatch);
        }

        protected virtual void HandleRemoteMessage(TransportMessage transportMessage, bool synchronous = false)
        {
            var dispatch = CreateMessageDispatch(transportMessage, synchronous);
            if (dispatch == null)
            {
                _transport.AckMessage(transportMessage);
                return;
            }

            _messageLogger.DebugFormat("RECV remote: {0} from {3} ({2} bytes). [{1}]", dispatch.Message, transportMessage.Id, transportMessage.Content.Length, transportMessage.Originator.SenderId);
            _messageDispatcher.Dispatch(dispatch);
        }

        private Action<MessageDispatch, DispatchResult> GetOnRemoteMessageDispatchedContinuation(TransportMessage transportMessage)
        {
            return (dispatch, dispatchResult) =>
            {
                HandleDispatchErrors(dispatch, dispatchResult, transportMessage);

                if (dispatch.Message is ICommand && !(dispatch.Message is PersistMessageCommand))
                {
                    var messageExecutionCompleted = MessageExecutionCompleted.Create(dispatch.Context, dispatchResult, _serializer);
                    var shouldLogMessageExecutionCompleted = _messageLogger.IsInfoEnabled(dispatch.Message);
                    SendTransportMessage(null, messageExecutionCompleted, dispatch.Context.GetSender(), shouldLogMessageExecutionCompleted);
                }

                AckTransportMessage(transportMessage);
            };
        }

        private void HandleDispatchErrors(MessageDispatch dispatch, DispatchResult dispatchResult, TransportMessage failingTransportMessage = null)
        {
            if (!_configuration.IsErrorPublicationEnabled || !IsRunning || dispatchResult.Errors.Count == 0 || dispatchResult.Errors.All(error => error is DomainException))
                return;

            var errorMessages = dispatchResult.Errors.Select(error => error.ToString());
            var errorMessage = string.Join(System.Environment.NewLine + System.Environment.NewLine, errorMessages);

            try
            {
                if (failingTransportMessage == null)
                    failingTransportMessage = ToTransportMessage(dispatch.Message);
            }
            catch (Exception ex)
            {
                HandleDispatchErrorsForUnserializableMessage(dispatch.Message, ex, errorMessage);
                return;
            }

            string jsonMessage;
            try
            {
                jsonMessage = JsonConvert.SerializeObject(dispatch.Message);
            }
            catch (Exception ex)
            {
                jsonMessage = $"Unable to serialize message :{System.Environment.NewLine}{ex}";
            }

            var messageProcessingFailed = new MessageProcessingFailed(failingTransportMessage, jsonMessage, errorMessage, SystemDateTime.UtcNow, dispatchResult.ErrorHandlerTypes.Select(x => x.FullName).ToArray());
            Publish(messageProcessingFailed);
        }

        private void HandleDispatchErrorsForUnserializableMessage(IMessage message, Exception serializationException, string dispatchErrorMessage)
        {
            var messageTypeName = message.GetType().FullName;
            _logger.Error($"Unable to serialize message {messageTypeName}. Error: {serializationException}");

            if (!_configuration.IsErrorPublicationEnabled || !IsRunning)
                return;

            var errorMessage = $"Unable to handle local message\r\nMessage is not serializable\r\nMessageType: {messageTypeName}\r\nDispatch error: {dispatchErrorMessage}\r\nSerialization error: {serializationException}";
            var processingFailed = new CustomProcessingFailed(GetType().FullName, errorMessage, SystemDateTime.UtcNow);
            Publish(processingFailed);
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
            _messageLogger.DebugFormat("RECV: {0}", message);

            TaskCompletionSource<CommandResult> taskCompletionSource;
            if (!_messageIdToTaskCompletionSources.TryRemove(message.SourceCommandId, out taskCompletionSource))
                return;

            var response = message.PayloadTypeId != null ? ToMessage(message.PayloadTypeId.Value, new MemoryStream(message.Payload), transportMessage) : null;
            var commandResult = new CommandResult(message.ErrorCode, message.ResponseMessage, response);

            Task.Run(() => taskCompletionSource.SetResult(commandResult));
        }

        protected virtual void HandleLocalMessage(IMessage message, TaskCompletionSource<CommandResult> taskCompletionSource)
        {
            _messageLogger.DebugFormat("RECV local: {0}", message);

            var context = MessageContext.CreateOverride(PeerId, EndPoint);
            var dispatch = new MessageDispatch(context, message, GetOnLocalMessageDispatchedContinuation(taskCompletionSource))
            {
                IsLocal = true
            };

            _messageDispatcher.Dispatch(dispatch);
        }

        private Action<MessageDispatch, DispatchResult> GetOnLocalMessageDispatchedContinuation(TaskCompletionSource<CommandResult> taskCompletionSource)
        {
            return (dispatch, dispatchResult) =>
            {
                HandleDispatchErrors(dispatch, dispatchResult);

                if (taskCompletionSource == null)
                    return;

                var errorStatus = dispatchResult.Errors.Any() ? CommandResult.GetErrorStatus(dispatchResult.Errors) : dispatch.Context.GetErrorStatus();
                var commandResult = new CommandResult(errorStatus.Code, errorStatus.Message, dispatch.Context.ReplyResponse);
                taskCompletionSource.SetResult(commandResult);
            };
        }

        private void SendTransportMessage(MessageId? messageId, IMessage message, Peer peer, bool logEnabled)
        {
            SendTransportMessage(messageId, message, new List<Peer>(1) { peer }, logEnabled);
        }

        private void SendTransportMessage(MessageId? messageId, IMessage message, IList<Peer> peers, bool logEnabled, bool locallyHandled = false)
        {
            if (peers.Count == 0)
            {
                if (!locallyHandled && logEnabled)
                    _messageLogger.InfoFormat("SEND: {0} with no target peer", message);

                return;
            }

            var transportMessage = ToTransportMessage(message);

            if (messageId != null)
                transportMessage.Id = messageId.Value;

            if (logEnabled)
                LogMessageSend(message, transportMessage, peers);

            SendTransportMessage(transportMessage, peers);
        }

        protected void SendTransportMessage(TransportMessage transportMessage, IList<Peer> peers)
        {
            _transport.Send(transportMessage, peers, new SendContext());
        }

        private void LogMessageSend(IMessage message, TransportMessage transportMessage, IList<Peer> peers)
        {
            _messageLogger.InfoFormat("SEND: {0} to {3} ({2} bytes) [{1}]", message, transportMessage.Id, transportMessage.Content.Length, peers);
        }

        protected void AckTransportMessage(TransportMessage transportMessage)
        {
            _transport.AckMessage(transportMessage);
        }

        protected TransportMessage ToTransportMessage(IMessage message)
        {
            return _serializer.ToTransportMessage(message, PeerId, EndPoint);
        }

        private IMessage ToMessage(TransportMessage transportMessage)
        {
            return ToMessage(transportMessage.MessageTypeId, transportMessage.Content, transportMessage);
        }

        private IMessage ToMessage(MessageTypeId messageTypeId, Stream messageStream, TransportMessage transportMessage)
        {
            try
            {
                return _serializer.ToMessage(transportMessage, messageTypeId, messageStream);
            }
            catch (Exception exception)
            {
                HandleDeserializationError(messageTypeId, messageStream, transportMessage.Originator, exception, transportMessage);
            }

            return null;
        }

        private void HandleDeserializationError(MessageTypeId messageTypeId, Stream messageStream, OriginatorInfo originator, Exception exception, TransportMessage transportMessage)
        {
            var dumpLocation = DumpMessageOnDisk(messageTypeId, messageStream);
            var errorMessage = $"Unable to deserialize message {messageTypeId.FullName}. Originator: {originator.SenderId}. Message dumped at: {dumpLocation}\r\n{exception}";
            _logger.Error(errorMessage);

            if (!_configuration.IsErrorPublicationEnabled || !IsRunning)
                return;

            var processingFailed = new MessageProcessingFailed(transportMessage, string.Empty, errorMessage, SystemDateTime.UtcNow, null);
            Publish(processingFailed);
        }

        private string DumpMessageOnDisk(MessageTypeId messageTypeId, Stream messageStream)
        {
            try
            {
                if (!System.IO.Directory.Exists(DeserializationFailureDumpDirectoryPath))
                    System.IO.Directory.CreateDirectory(DeserializationFailureDumpDirectoryPath);

                var dumpFileName = $"{_deserializationFailureTimestampProvider.NextUtcTimestamp():yyyyMMdd-HH-mm-ss.fffffff}_{messageTypeId.FullName}";
                var dumpFilePath = Path.Combine(DeserializationFailureDumpDirectoryPath, dumpFileName);
                messageStream.Seek(0, SeekOrigin.Begin);
                using (var fileStream = new FileStream(dumpFilePath, FileMode.Create))
                {
                    messageStream.CopyTo(fileStream);
                }

                return dumpFilePath;
            }
            catch (Exception ex)
            {
                return "Message could not be dumped: " + ex;
            }
        }

        public void Dispose()
        {
            if (IsRunning)
                Stop();
        }
    }
}
