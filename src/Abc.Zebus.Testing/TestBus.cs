using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Serialization;
using Abc.Zebus.Testing.Comparison;
using Abc.Zebus.Util;
using Abc.Zebus.Util.Extensions;

namespace Abc.Zebus.Testing
{
    public class TestBus : IBus, IInternalBus
    {
        private readonly ConcurrentDictionary<HandlerKey, Func<IMessage, object?>> _handlers = new ConcurrentDictionary<HandlerKey, Func<IMessage, object?>>();
        private readonly MessageComparer _messageComparer = new MessageComparer();
        private readonly Dictionary<PeerId, List<IMessage>> _messagesByPeerId = new Dictionary<PeerId, List<IMessage>>();
        private readonly Dictionary<ICommand, Peer?> _peerByCommand = new Dictionary<ICommand, Peer?>();
        private readonly List<IEvent> _events = new List<IEvent>();
        private readonly List<ICommand> _commands = new List<ICommand>();
        private readonly HashSet<Subscription> _subscriptions = new HashSet<Subscription>();

        public TestBus()
        {
            MessageSerializer = new MessageSerializer();
            HandlerExecutor = new DefaultHandlerExecutor();
        }

        public event Action Starting = delegate { };
        public event Action Started = delegate { };
        public event Action Stopping = delegate { };
        public event Action Stopped = delegate { };

        public IEnumerable<ICommand> Commands
        {
            get
            {
                lock (_commands)
                {
                    return _commands.ToList();
                }
            }
        }

        public IEnumerable<IEvent> Events
        {
            get
            {
                lock (_events)
                {
                    return _events.ToList();
                }
            }
        }

        public int MessageCount => _commands.Count + _events.Count;

        public HashSet<Subscription> Subscriptions
        {
            get
            {
                lock (_subscriptions)
                {
                    return new HashSet<Subscription>(_subscriptions);
                }
            }
        }

        public int LastReplyCode { get; set; }
        public string? LastReplyMessage { get; set; }
        public object? LastReplyResponse { get; set; }
        public IHandlerExecutor HandlerExecutor { get; set; }
        public IMessageSerializer MessageSerializer { get; set; }
        public bool IsStarted { get; private set; }
        public bool IsStopped { get; private set; }
        public PeerId PeerId { get; private set; }
        public string Environment { get; private set; } = string.Empty;
        public bool IsRunning { get; private set; }

        public IEnumerable<IMessage> Messages => Events.Cast<IMessage>().Concat(Commands);

        public void Publish(IEvent message)
        {
            Publish(message, null);
        }

        public void Publish(IEvent message, PeerId targetPeer)
        {
            Publish(message, (PeerId?)targetPeer);
        }

        private void Publish(IEvent message, PeerId? targetPeer)
        {
            if (MessageSerializer.TryClone(message, out var clone))
                message = (IEvent)clone;

            lock (_events)
            {
                _events.Add(message);
                if (targetPeer != null)
                    _messagesByPeerId.GetValueOrAdd(targetPeer.Value, p => new List<IMessage>()).Add(message);
            }

            if (_handlers.TryGetValue(new HandlerKey(message.GetType(), default), out var handler))
                handler.Invoke(message);
        }

        public Task<CommandResult> Send(ICommand message)
        {
            return Send(message, null);
        }

        public Task<CommandResult> Send(ICommand message, Peer? peer)
        {
            if (MessageSerializer.TryClone(message, out var clone))
                message = (ICommand)clone;

            lock (_commands)
            {
                _commands.Add(message);
                if (peer != null)
                    _messagesByPeerId.GetValueOrAdd(peer.Id, p => new List<IMessage>()).Add(message);

                _peerByCommand[message] = peer;
            }

            Func<IMessage, object?>? handler;

            if (peer != null)
                _handlers.TryGetValue(new HandlerKey(message.GetType(), peer.Id), out handler);
            else
                handler = null;

            // TODO why do we fall back in all cases?
            if (handler == null)
                _handlers.TryGetValue(new HandlerKey(message.GetType(), default), out handler);

            return HandlerExecutor.Execute(message, handler);
        }

        public async Task<IDisposable> SubscribeAsync(SubscriptionRequest request)
        {
            request.MarkAsSubmitted(0);

            if (request.Batch != null)
                await request.Batch.WhenSubmittedAsync().ConfigureAwait(false);

            await AddSubscriptionsAsync(request).ConfigureAwait(false);

            return new DisposableAction(() => RemoveSubscriptions(request));
        }

        public async Task<IDisposable> SubscribeAsync(SubscriptionRequest request, Action<IMessage> handler)
        {
            request.MarkAsSubmitted(0);

            if (request.Batch != null)
                await request.Batch.WhenSubmittedAsync().ConfigureAwait(false);

            var handlerKeys = request.Subscriptions.Select(x => new HandlerKey(x.MessageTypeId.GetMessageType()!, default)).ToList();

            foreach (var handlerKey in handlerKeys)
            {
                _handlers[handlerKey] = x =>
                {
                    handler(x);
                    return null;
                };
            }

            await AddSubscriptionsAsync(request).ConfigureAwait(false);

            return new DisposableAction(() =>
            {
                RemoveSubscriptions(request);
                _handlers.RemoveRange(handlerKeys);
            });
        }

        private async Task AddSubscriptionsAsync(SubscriptionRequest request)
        {
            if (request.Batch != null)
            {
                var batchSubscriptions = request.Batch.TryConsumeBatchSubscriptions();
                if (batchSubscriptions != null)
                {
                    AddSubscriptions(batchSubscriptions);
                    request.Batch.NotifyRegistrationCompleted(null);
                }
                else
                {
                    await request.Batch.WhenRegistrationCompletedAsync().ConfigureAwait(false);
                }
            }
            else
            {
                AddSubscriptions(request.Subscriptions);
            }

            void AddSubscriptions(IEnumerable<Subscription> subscriptions)
            {
                lock (_subscriptions)
                {
                    _subscriptions.AddRange(subscriptions);
                }
            }
        }

        private void RemoveSubscriptions(SubscriptionRequest request)
        {
            lock (_subscriptions)
            {
                _subscriptions.RemoveRange(request.Subscriptions);
            }
        }

        public void Reply(int errorCode)
        {
            LastReplyCode = errorCode;
            LastReplyMessage = null;
        }

        public void Reply(int errorCode, string? message)
        {
            LastReplyCode = errorCode;
            LastReplyMessage = message;
        }

        public void Reply(IMessage? response)
        {
            LastReplyResponse = response;
        }

        public void Configure(PeerId peerId, string environment = "test")
        {
            PeerId = peerId;
            Environment = environment;
        }

        public void Start()
        {
            Starting();
            IsStarted = true;
            IsRunning = true;
            Started();
        }

        public void Stop()
        {
            Stopping();
            IsStopped = true;
            IsRunning = false;
            Stopped();
        }

        public void AddHandler<TMessage>(Func<TMessage, object?> handler)
            where TMessage : IMessage
        {
            _handlers[new HandlerKey(typeof(TMessage), default)] = x => handler((TMessage)x);
        }

        public void AddSuccessfulHandler<TMessage>()
            where TMessage : IMessage
        {
            _handlers[new HandlerKey(typeof(TMessage), default)] = x => true;
        }

        public void AddHandlerForPeer<TMessage>(PeerId peerId, Func<TMessage, object?> handler)
            where TMessage : IMessage
        {
            _handlers[new HandlerKey(typeof(TMessage), peerId)] = x => handler((TMessage)x);
        }

        public void AddHandlerForPeer<TMessage>(PeerId peerId, Action<TMessage> handler)
            where TMessage : IMessage
        {
            _handlers[new HandlerKey(typeof(TMessage), peerId)] = x =>
            {
                handler((TMessage)x);
                return null;
            };
        }

        public void AddHandler<TMessage>(Action<TMessage> handler)
            where TMessage : IMessage
        {
            _handlers[new HandlerKey(typeof(TMessage), default)] = x =>
            {
                handler((TMessage)x);
                return null;
            };
        }

        public void AddHandlerThatThrowsDomainException<TMessage>(DomainException ex)
            where TMessage : IMessage
        {
            _handlers[new HandlerKey(typeof(TMessage), default)] = _ => throw ex;
        }

        public void AddHandlerThatThrowsMessageProcessingException<TMessage>(MessageProcessingException ex)
            where TMessage : IMessage
        {
            _handlers[new HandlerKey(typeof(TMessage), default)] = _ => throw ex;
        }

        public void AddHandlerThatThrows<TMessage>(Exception? ex = null)
            where TMessage : IMessage
        {
            _handlers[new HandlerKey(typeof(TMessage), default)] = _ => throw (ex ?? new Exception());
        }

        public void Expect(IEnumerable<IMessage> expectedMessages)
        {
            _messageComparer.CheckExpectations(Messages, expectedMessages, false);
        }

        public void Expect(params IMessage[] expectedMessages)
        {
            _messageComparer.CheckExpectations(Messages, expectedMessages, false);
        }

        public void ExpectExactly(params IMessage[] expectedMessages)
        {
            _messageComparer.CheckExpectations(Messages, expectedMessages, true);
        }

        public void ExpectExactly(PeerId peerId, params IMessage[] expectedMessages)
        {
            var messages = _messagesByPeerId.GetValueOrDefault(peerId, p => new List<IMessage>());
            _messageComparer.CheckExpectations(messages, expectedMessages, true);
        }

        public void ExpectNothing()
        {
            _messageComparer.CheckExpectations(Messages, new List<object>(), true);
        }

        public IList<PeerId> GetContactedPeerIds()
        {
            return _messagesByPeerId.Keys.ToList();
        }

        public Peer? GetRecipientPeer(ICommand command)
        {
            return _peerByCommand[command];
        }

        /// <summary>
        /// Executes handler synchronously (simulate locally handled messages).
        /// </summary>
        public class DefaultHandlerExecutor : IHandlerExecutor
        {
            public Task<CommandResult> Execute(ICommand command, Func<IMessage, object?>? handler)
            {
                var taskCompletionSource = new TaskCompletionSource<CommandResult>();
                try
                {
                    var result = handler?.Invoke(command);
                    taskCompletionSource.SetResult(new CommandResult(0, null, result));
                }
                catch (MessageProcessingException ex)
                {
                    taskCompletionSource.SetResult(new CommandResult(ex.ErrorCode, ex.Message, null));
                }
                catch (Exception)
                {
                    taskCompletionSource.SetResult(new CommandResult(1, null, null));
                }

                return taskCompletionSource.Task;
            }
        }

        /// <summary>
        /// Executes handler asynchronously (simulate remotely handled messages or messages handled in
        /// a different dispatch queue).
        /// </summary>
        public class AsyncHandlerExecutor : IHandlerExecutor
        {
            public Task<CommandResult> Execute(ICommand command, Func<IMessage, object?>? handler)
            {
                return Task.Run(() =>
                {
                    try
                    {
                        var response = handler?.Invoke(command);
                        return new CommandResult(0, null, response);
                    }
                    catch (MessageProcessingException ex)
                    {
                        return new CommandResult(ex.ErrorCode, ex.Message, null);
                    }
                    catch (Exception ex)
                    {
                        return new CommandResult(1, ex.Message, null);
                    }
                });
            }
        }

        /// <summary>
        /// Never executes handler (simulate down peer).
        /// </summary>
        public class DoNotReplyHandlerExecutor : IHandlerExecutor
        {
            public Task<CommandResult> Execute(ICommand command, Func<IMessage, object?>? handler)
            {
                return new TaskCompletionSource<CommandResult>().Task;
            }
        }

        public interface IHandlerExecutor
        {
            Task<CommandResult> Execute(ICommand command, Func<IMessage, object?>? handler);
        }

        private class HandlerKey : Tuple<Type, PeerId>
        {
            public HandlerKey(Type type, PeerId peerId)
                : base(type, peerId)
            {
            }
        }

        public IDisposable InjectSubscription(Subscription subscription)
            => this.Subscribe(subscription);

        public void Dispose()
        {
            Stop();
        }

        public void ClearMessages()
        {
            lock (_commands)
            {
                _commands.Clear();
                _peerByCommand.Clear();
                _messagesByPeerId.Clear();
            }

            lock (_events)
            {
                _events.Clear();
            }
        }
    }
}
