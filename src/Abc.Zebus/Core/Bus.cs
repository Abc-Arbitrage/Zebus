using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Directory;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Lotus;
using Abc.Zebus.Persistence;
using Abc.Zebus.Serialization;
using Abc.Zebus.Subscriptions;
using Abc.Zebus.Transport;
using Abc.Zebus.Util;
using Abc.Zebus.Util.Extensions;
using log4net;
using Newtonsoft.Json;

namespace Abc.Zebus.Core
{
    public class Bus : IInternalBus, IMessageDispatchFactory
    {
        private static readonly BusMessageLogger _messageLogger = new(typeof(Bus));
        private static readonly ILog _logger = LogManager.GetLogger(typeof(Bus));

        private readonly ConcurrentDictionary<MessageId, TaskCompletionSource<CommandResult>> _messageIdToTaskCompletionSources = new();
        private readonly UniqueTimestampProvider _deserializationFailureTimestampProvider = new();
        private readonly Dictionary<Subscription, int> _subscriptions = new();
        private readonly HashSet<MessageTypeId> _pendingUnsubscriptions = new();
        private readonly ITransport _transport;
        private readonly IPeerDirectory _directory;
        private readonly IMessageSerializer _serializer;
        private readonly IMessageDispatcher _messageDispatcher;
        private readonly IMessageSendingStrategy _messageSendingStrategy;
        private readonly IStoppingStrategy _stoppingStrategy;
        private readonly IBusConfiguration _configuration;

        private Task? _processPendingUnsubscriptionsTask;
        private TaskCompletionSource<object?>? _busStartedTcs;

        private int _subscriptionsVersion;
        private int _status;

        public Bus(ITransport transport,
                   IPeerDirectory directory,
                   IMessageSerializer serializer,
                   IMessageDispatcher messageDispatcher,
                   IMessageSendingStrategy messageSendingStrategy,
                   IStoppingStrategy stoppingStrategy,
                   IBusConfiguration configuration)
        {
            _transport = transport;
            _transport.MessageReceived += OnTransportMessageReceived;
            _directory = directory;
            _directory.PeerUpdated += OnPeerUpdated;
            _directory.PeerSubscriptionsUpdated += DispatchSubscriptionsUpdatedMessages;
            _serializer = serializer;
            _messageDispatcher = messageDispatcher;
            _messageDispatcher.MessageHandlerInvokersUpdated += MessageDispatcherOnMessageHandlerInvokersUpdated;
            _messageSendingStrategy = messageSendingStrategy;
            _stoppingStrategy = stoppingStrategy;
            _configuration = configuration;
        }

        public event Action? Starting;
        public event Action? Started;
        public event Action? Stopping;
        public event Action? Stopped;

        public PeerId PeerId { get; private set; }
        public string Environment { get; private set; } = string.Empty;
        public bool IsRunning => Status == BusStatus.Started || Status == BusStatus.Stopping;
        public string EndPoint => _transport.InboundEndPoint;
        public string DeserializationFailureDumpDirectoryPath { get; set; } = PathUtil.InBaseDirectory("deserialization_failure_dumps");

        private BusStatus Status
        {
            get => (BusStatus)_status;
            set => _status = (int)value;
        }

        public void Configure(PeerId peerId, string environment)
        {
            PeerId = peerId;
            Environment = environment;
            _transport.Configure(peerId, environment);
        }

        public virtual void Start()
        {
            if (Interlocked.CompareExchange(ref _status, (int)BusStatus.Starting, (int)BusStatus.Stopped) != (int)BusStatus.Stopped)
                throw new InvalidOperationException("Unable to start, the bus is already running");

            try
            {
                Starting?.Invoke();
            }
            catch
            {
                Status = BusStatus.Stopped;
                throw;
            }

            var registered = false;
            try
            {
                _logger.DebugFormat("Loading invokers...");
                _messageDispatcher.LoadMessageHandlerInvokers();

                PerformStartupSubscribe();

                _logger.DebugFormat("Starting transport...");
                _transport.Start();

                Status = BusStatus.Started;

                _logger.DebugFormat("Registering on directory...");
                var self = new Peer(PeerId, EndPoint);
                _directory.RegisterAsync(this, self, GetSubscriptions()).Wait();
                registered = true;

                _transport.OnRegistered();

                _logger.DebugFormat("Starting message dispatcher...");
                _messageDispatcher.Start();
            }
            catch (Exception ex)
            {
                InternalStop(registered);
                Status = BusStatus.Stopped;
                Interlocked.Exchange(ref _busStartedTcs, null)?.TrySetException(ex);
                throw;
            }

            Interlocked.Exchange(ref _busStartedTcs, null)?.TrySetResult(null);
            Started?.Invoke();
        }

        private void PerformStartupSubscribe()
        {
            _logger.DebugFormat("Performing startup subscribe...");

            var autoSubscribeInvokers = _messageDispatcher.GetMessageHandlerInvokers().Where(x => x.ShouldBeSubscribedOnStartup).ToList();

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
            if (Interlocked.CompareExchange(ref _status, (int)BusStatus.Stopping, (int)BusStatus.Started) != (int)BusStatus.Started)
                throw new InvalidOperationException("Unable to stop, the bus is not running");

            try
            {
                Stopping?.Invoke();
            }
            catch
            {
                Status = BusStatus.Started;
                throw;
            }

            InternalStop(true);

            Stopped?.Invoke();
        }

        private void InternalStop(bool unregister)
        {
            Status = BusStatus.Stopping;

            if (unregister)
            {
                try
                {
                    _directory.UnregisterAsync(this).Wait();
                }
                catch (Exception ex)
                {
                    _logger.Error(ex);
                }
            }

            lock (_subscriptions)
            {
                _subscriptions.Clear();
                _pendingUnsubscriptions.Clear();
                _processPendingUnsubscriptionsTask = null;
                _busStartedTcs = null;

                unchecked
                {
                    ++_subscriptionsVersion;
                }
            }

            try
            {
                _stoppingStrategy.Stop(_transport, _messageDispatcher);
            }
            finally
            {
                Status = BusStatus.Stopped;
            }

            _messageIdToTaskCompletionSources.Clear();
        }

        public void Publish(IEvent message)
        {
            if (!IsRunning)
                throw new InvalidOperationException("Unable to publish message, the bus is not running");

            var peersHandlingMessage = _directory.GetPeersHandlingMessage(message);

            PublishImpl(message, peersHandlingMessage);
        }

        public void Publish(IEvent message, PeerId targetPeerId)
        {
            if (!IsRunning)
                throw new InvalidOperationException("Unable to publish message, the bus is not running");

            var peer = _directory.GetPeer(targetPeerId);
            if (peer != null)
                PublishImpl(message, new List<Peer> { peer });
        }

        private void PublishImpl(IEvent message, IList<Peer> peers)
        {
            var localDispatchEnabled = LocalDispatch.Enabled;
            var shouldBeHandledLocally = localDispatchEnabled && peers.Any(x => x.Id == PeerId);
            if (shouldBeHandledLocally)
                HandleLocalMessage(message, null);

            var targetPeers = shouldBeHandledLocally ? peers.Where(x => x.Id != PeerId).ToList() : peers;
            LogAndSendMessage(message, targetPeers, true, shouldBeHandledLocally);
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
                return SendImpl(message, self);

            if (peers.Count > 1)
            {
                var exceptionMessage = $"{peers.Count} peers are handling {BusMessageLogger.ToString(message)}. Peers: {string.Join(", ", peers.Select(p => p.ToString()))}.";
                throw new InvalidOperationException(exceptionMessage);
            }

            return SendImpl(message, peers[0]);
        }

        public Task<CommandResult> Send(ICommand message, Peer peer)
        {
            if (peer == null)
                throw new ArgumentNullException(nameof(peer));

            if (!IsRunning)
                throw new InvalidOperationException("Unable to send message, the bus is not running");

            return SendImpl(message, peer);
        }

        private Task<CommandResult> SendImpl(ICommand message, Peer peer)
        {
            var taskCompletionSource = new TaskCompletionSource<CommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);

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
                _messageLogger.LogSendMessage(message, peers, transportMessage);
                SendTransportMessage(transportMessage, peers);
            }

            return taskCompletionSource.Task;
        }

        public async Task<IDisposable> SubscribeAsync(SubscriptionRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (IsRunning && !request.ThereIsNoHandlerButIKnowWhatIAmDoing)
                EnsureMessageHandlerInvokerExists(request.Subscriptions);

            request.MarkAsSubmitted();

            if (request.Batch != null)
                await request.Batch.WhenSubmittedAsync().ConfigureAwait(false);

            await AddSubscriptionsAsync(request).ConfigureAwait(false);

            return new DisposableAction(() => RemoveSubscriptions(request));
        }

        public async Task<IDisposable> SubscribeAsync(SubscriptionRequest request, Action<IMessage> handler)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            request.MarkAsSubmitted();

            var eventHandlerInvokers = request.Subscriptions
                                              .GroupBy(x => x.MessageTypeId)
                                              .Select(x => new DynamicMessageHandlerInvoker(
                                                          handler,
                                                          x.Key.GetMessageType() ?? throw new InvalidOperationException($"Could not resolve type {x.Key.FullName}"),
                                                          x.Select(s => s.BindingKey).ToList()
                                                      ))
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

        private void EnsureMessageHandlerInvokerExists(IEnumerable<Subscription> subscriptions)
        {
            foreach (var subscription in subscriptions)
            {
                if (_messageDispatcher.GetMessageHandlerInvokers().All(x => x.MessageTypeId != subscription.MessageTypeId))
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
                var updatedTypes = new HashSet<MessageTypeId>();

                lock (_subscriptions)
                {
                    foreach (var subscription in subscriptions)
                    {
                        var subscriptionCount = _subscriptions.GetValueOrDefault(subscription);
                        _subscriptions[subscription] = subscriptionCount + 1;

                        if (subscriptionCount <= 0)
                            updatedTypes.Add(subscription.MessageTypeId);
                    }
                }

                if (IsRunning)
                {
                    if (updatedTypes.Count != 0)
                    {
                        // Wait until all unsubscriptions are completed to prevent race conditions
                        await WhenUnsubscribeCompletedAsync().ConfigureAwait(false);
                        await UpdateDirectorySubscriptionsAsync(updatedTypes).ConfigureAwait(false);
                    }
                }
                else
                {
                    await WhenBusStartedAsync().ConfigureAwait(false);
                }
            }
        }

        private Task WhenBusStartedAsync()
        {
            if (IsRunning)
                return Task.CompletedTask;

            var tcs = Volatile.Read(ref _busStartedTcs);

            if (tcs is null)
            {
                tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
                tcs = Interlocked.CompareExchange(ref _busStartedTcs, tcs, null) ?? tcs;
            }

            return IsRunning ? Task.CompletedTask : tcs.Task;
        }

        internal async Task WhenUnsubscribeCompletedAsync()
        {
            Task? task;

            lock (_subscriptions)
            {
                task = _processPendingUnsubscriptionsTask;
            }

            if (task == null)
                return;

            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
        }

        private void RemoveSubscriptions(SubscriptionRequest request)
        {
            lock (_subscriptions)
            {
                if (request.SubmissionSubscriptionsVersion != _subscriptionsVersion)
                    return;

                foreach (var subscription in request.Subscriptions)
                {
                    var subscriptionCount = _subscriptions.GetValueOrDefault(subscription);
                    if (subscriptionCount <= 1)
                    {
                        _subscriptions.Remove(subscription);

                        if (IsRunning)
                            _pendingUnsubscriptions.Add(subscription.MessageTypeId);
                    }
                    else
                    {
                        _subscriptions[subscription] = subscriptionCount - 1;
                    }
                }

                if (_pendingUnsubscriptions.Count != 0 && _processPendingUnsubscriptionsTask?.IsCompleted != false)
                {
                    var subscriptionsVersion = _subscriptionsVersion;
                    _processPendingUnsubscriptionsTask = Task.Run(() => ProcessPendingUnsubscriptions(subscriptionsVersion));
                }
            }
        }

        private async Task ProcessPendingUnsubscriptions(int subscriptionsVersion)
        {
            try
            {
                var updatedTypes = new HashSet<MessageTypeId>();

                while (true)
                {
                    updatedTypes.Clear();

                    lock (_subscriptions)
                    {
                        updatedTypes.UnionWith(_pendingUnsubscriptions);
                        _pendingUnsubscriptions.Clear();

                        if (updatedTypes.Count == 0 || !IsRunning || Status == BusStatus.Stopping || subscriptionsVersion != _subscriptionsVersion)
                        {
                            _processPendingUnsubscriptionsTask = null;
                            return;
                        }
                    }

                    await UpdateDirectorySubscriptionsAsync(updatedTypes).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex);

                lock (_subscriptions)
                {
                    _processPendingUnsubscriptionsTask = null;
                }
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

        public void Reply(int errorCode)
            => Reply(errorCode, null);

        public void Reply(int errorCode, string? message)
        {
            var messageContext = MessageContext.Current;
            if (messageContext == null)
                throw new InvalidOperationException("Reply called without message context");

            messageContext.ReplyCode = errorCode;
            messageContext.ReplyMessage = message;
        }

        public void Reply(IMessage? response)
        {
            var messageContext = MessageContext.Current;
            if (messageContext == null)
                throw new InvalidOperationException("Reply called without message context");

            messageContext.ReplyResponse = response;
        }

        private void OnPeerUpdated(PeerId peerId, PeerUpdateAction peerUpdateAction)
            => _transport.OnPeerUpdated(peerId, peerUpdateAction);

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

        public MessageDispatch? CreateMessageDispatch(TransportMessage transportMessage)
            => CreateMessageDispatch(transportMessage, synchronousDispatch: false, sendAcknowledgment: false);

        private MessageDispatch? CreateMessageDispatch(TransportMessage transportMessage, bool synchronousDispatch, bool sendAcknowledgment = true)
        {
            var message = ToMessage(transportMessage);
            if (message == null)
                return null;

            var context = MessageContext.CreateNew(transportMessage);
            var continuation = GetOnRemoteMessageDispatchedContinuation(transportMessage, sendAcknowledgment);
            return new MessageDispatch(context, message, _serializer, continuation, synchronousDispatch);
        }

        protected virtual void HandleRemoteMessage(TransportMessage transportMessage, bool synchronous = false)
        {
            var dispatch = CreateMessageDispatch(transportMessage, synchronous);
            if (dispatch == null)
            {
                _logger.WarnFormat("Received a remote message that could not be deserialized: {0} from {1}", transportMessage.MessageTypeId.FullName, transportMessage.Originator.SenderId);
                _transport.AckMessage(transportMessage);
                return;
            }

            _messageLogger.LogReceiveMessageRemote(dispatch.Message, transportMessage);
            _messageDispatcher.Dispatch(dispatch);
        }

        private Action<MessageDispatch, DispatchResult> GetOnRemoteMessageDispatchedContinuation(TransportMessage transportMessage, bool sendAcknowledgment)
        {
            return (dispatch, dispatchResult) =>
            {
                HandleDispatchErrors(dispatch, dispatchResult, transportMessage);

                if (!sendAcknowledgment)
                    return;

                if (dispatch.Message is ICommand && !(dispatch.Message is PersistMessageCommand))
                {
                    var messageExecutionCompleted = MessageExecutionCompleted.Create(dispatch.Context, dispatchResult, _serializer);
                    var shouldLogMessageExecutionCompleted = _messageLogger.IsInfoEnabled(dispatch.Message);
                    LogAndSendMessage(messageExecutionCompleted, new[] { dispatch.Context.GetSender() }, shouldLogMessageExecutionCompleted);
                }

                AckTransportMessage(transportMessage);
            };
        }

        private void HandleDispatchErrors(MessageDispatch dispatch, DispatchResult dispatchResult, TransportMessage? failingTransportMessage = null)
        {
            if (!_configuration.IsErrorPublicationEnabled || !IsRunning || dispatchResult.Errors.Count == 0 || dispatchResult.Errors.All(error => error is MessageProcessingException { ShouldPublishError: false }))
                return;

            var errorMessages = dispatchResult.Errors.Select(error => error.ToString());
            var errorMessage = string.Join(System.Environment.NewLine + System.Environment.NewLine, errorMessages);

            try
            {
                failingTransportMessage ??= ToTransportMessage(dispatch.Message);
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

            var messageProcessingFailed = new MessageProcessingFailed(failingTransportMessage, jsonMessage, errorMessage, SystemDateTime.UtcNow, dispatchResult.ErrorHandlerTypes.Select(x => x.FullName!).ToArray());
            Publish(messageProcessingFailed);
        }

        private void HandleDispatchErrorsForUnserializableMessage(IMessage message, Exception serializationException, string dispatchErrorMessage)
        {
            var messageTypeName = message.GetType().FullName;
            _logger.Error($"Unable to serialize message {messageTypeName}. Error: {serializationException}");

            if (!_configuration.IsErrorPublicationEnabled || !IsRunning)
                return;

            var errorMessage = $"Unable to handle local message\r\nMessage is not serializable\r\nMessageType: {messageTypeName}\r\nDispatch error: {dispatchErrorMessage}\r\nSerialization error: {serializationException}";
            var processingFailed = new CustomProcessingFailed(GetType().FullName!, errorMessage, SystemDateTime.UtcNow);
            Publish(processingFailed);
        }

        private void HandleMessageExecutionCompleted(TransportMessage transportMessage)
        {
            var message = (MessageExecutionCompleted?)ToMessage(transportMessage);
            if (message == null)
                return;

            HandleMessageExecutionCompleted(transportMessage, message);
        }

        protected virtual void HandleMessageExecutionCompleted(TransportMessage transportMessage, MessageExecutionCompleted message)
        {
            _messageLogger.LogReceiveMessageAck(message);

            if (!_messageIdToTaskCompletionSources.TryRemove(message.SourceCommandId, out var taskCompletionSource))
                return;

            var response = message.PayloadTypeId != null ? ToMessage(message.PayloadTypeId.Value, new MemoryStream(message.Payload ?? Array.Empty<byte>()), transportMessage) : null;
            var commandResult = new CommandResult(message.ErrorCode, message.ResponseMessage, response);

            taskCompletionSource.SetResult(commandResult);
        }

        protected virtual void HandleLocalMessage(IMessage message, TaskCompletionSource<CommandResult>? taskCompletionSource)
        {
            _messageLogger.LogReceiveMessageLocal(message);

            var context = MessageContext.CreateOverride(PeerId, EndPoint);
            var dispatch = new MessageDispatch(context, message, _serializer, GetOnLocalMessageDispatchedContinuation(taskCompletionSource))
            {
                IsLocal = true
            };

            _messageDispatcher.Dispatch(dispatch);
        }

        private Action<MessageDispatch, DispatchResult> GetOnLocalMessageDispatchedContinuation(TaskCompletionSource<CommandResult>? taskCompletionSource)
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

        private void LogAndSendMessage(IMessage message, IList<Peer> peers, bool logEnabled, bool locallyHandled = false)
        {
            if (peers.Count == 0)
            {
                if (!locallyHandled && logEnabled)
                    _messageLogger.LogSendMessage(message, peers);

                return;
            }

            var transportMessage = ToTransportMessage(message);

            if (logEnabled)
                _messageLogger.LogSendMessage(message, peers, transportMessage);

            SendTransportMessage(transportMessage, peers);
        }

        protected void SendTransportMessage(TransportMessage transportMessage, IList<Peer> peers)
            => _transport.Send(transportMessage, peers, new SendContext());

        protected void AckTransportMessage(TransportMessage transportMessage)
            => _transport.AckMessage(transportMessage);

        protected TransportMessage ToTransportMessage(IMessage message)
            => _serializer.ToTransportMessage(message, PeerId, EndPoint);

        private IMessage? ToMessage(TransportMessage transportMessage)
            => ToMessage(transportMessage.MessageTypeId, transportMessage.Content, transportMessage);

        private IMessage? ToMessage(MessageTypeId messageTypeId, Stream? messageStream, TransportMessage transportMessage)
        {
            if (messageStream is null)
                return null;

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

        private void MessageDispatcherOnMessageHandlerInvokersUpdated()
        {
            var snapshotGeneratingMessageTypes = _messageDispatcher.GetMessageHandlerInvokers()
                                                                   .Select(x => x.MessageHandlerType.GetBaseTypes().SingleOrDefault(y => y.IsGenericType && y.GetGenericTypeDefinition() == typeof(SubscriptionHandler<>))?.GenericTypeArguments[0])
                                                                   .Where(x => x != null);

            _directory.EnableSubscriptionsUpdatedFor(snapshotGeneratingMessageTypes!);
        }

        private void DispatchSubscriptionsUpdatedMessages(PeerId peerId, IReadOnlyList<Subscription> subscriptions)
        {
            if (peerId == PeerId)
                return;

            var messageContext = GetMessageContextForSubscriptionsUpdated();
            foreach (var subscription in subscriptions)
            {
                var subscriptionsUpdated = new SubscriptionsUpdated(subscription, peerId);
                var dispatch = new MessageDispatch(messageContext, subscriptionsUpdated, _serializer, GetOnLocalMessageDispatchedContinuation(null));
                _messageDispatcher.Dispatch(dispatch);
            }
        }

        private MessageContext GetMessageContextForSubscriptionsUpdated()
            => MessageContext.Current ?? MessageContext.CreateOverride(PeerId, EndPoint);

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
            if (Status == BusStatus.Started)
                Stop();
        }

        private enum BusStatus
        {
            Stopped,
            Stopping,
            Starting,
            Started
        }
    }
}
