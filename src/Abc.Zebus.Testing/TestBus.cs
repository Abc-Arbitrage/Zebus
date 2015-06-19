using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Testing.Comparison;
using Abc.Zebus.Util;
using Abc.Zebus.Util.Extensions;

namespace Abc.Zebus.Testing
{
    public class TestBus : IBus
    {
        private readonly ConcurrentDictionary<HandlerKey, Func<IMessage, object>> _handlers = new ConcurrentDictionary<HandlerKey, Func<IMessage, object>>();
        private readonly MessageComparer _messageComparer = new MessageComparer();
        private readonly Dictionary<PeerId, List<IMessage>> _messagesByPeerId = new Dictionary<PeerId, List<IMessage>>();
        private readonly Dictionary<ICommand, Peer> _peerByCommand = new Dictionary<ICommand, Peer>();
        private readonly List<IEvent> _events = new List<IEvent>();
        private readonly List<ICommand> _commands = new List<ICommand>();
        private readonly HashSet<Subscription> _subscriptions = new HashSet<Subscription>();

        public TestBus()
        {
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

        public int MessageCount
        {
            get { return _commands.Count + _events.Count; }
        }

        public HashSet<Subscription> Subscriptions
        {
            get
            {
                lock (_subscriptions)
                {
                    return _subscriptions.ToHashSet();
                }
            }
        }

        public int LastReplyCode { get; set; }
        public object LastReplyResponse { get; set; }
        public IHandlerExecutor HandlerExecutor { get; set; }
        public bool IsStarted { get; private set; }
        public bool IsStopped { get; private set; }
        public PeerId PeerId { get; private set; }
        public string Environment { get; private set; }
        public bool IsRunning { get; private set; }

        public IEnumerable<IMessage> Messages
        {
            get { return Events.Cast<IMessage>().Concat(Commands); }
        }

        public void Publish(IEvent message)
        {
            lock (_events)
            {
                _events.Add(message);
            }
        }

        public Task<CommandResult> Send(ICommand message)
        {
            return Send(message, null);
        }

        public Task<CommandResult> Send(ICommand message, Peer peer)
        {
            lock (_commands)
            {
                _commands.Add(message);
                if (peer != null)
                    _messagesByPeerId.GetValueOrAdd(peer.Id, p => new List<IMessage>()).Add(message);

                _peerByCommand[message] = peer;
            }

            var handler = (peer != null) ? _handlers.GetValueOrDefault(new HandlerKey(message.GetType(), peer.Id)) : null;
            if (handler == null)
                handler = _handlers.GetValueOrDefault(new HandlerKey(message.GetType(), default(PeerId)));

            return HandlerExecutor.Execute(message, handler);
        }

        public IDisposable Subscribe(Subscription subscription, SubscriptionOptions options = SubscriptionOptions.Default)
        {
            lock (_subscriptions)
            {
                _subscriptions.Add(subscription);
            }

            return new DisposableAction(() =>
            {
                lock (_subscriptions)
                {
                    _subscriptions.Remove(subscription);
                }
            });
        }

        public IDisposable Subscribe(Subscription[] subscriptions, SubscriptionOptions options = SubscriptionOptions.Default)
        {
            lock (_subscriptions)
            {
                _subscriptions.AddRange(subscriptions);
            }

            return new DisposableAction(() =>
            {
                lock (_subscriptions)
                {
                    _subscriptions.RemoveRange(subscriptions);
                }
            });
        }

        public IDisposable Subscribe<T>(Action<T> handler) where T : class, IMessage
        {
            var handlerKey = new HandlerKey(typeof(T), default(PeerId));

            _handlers[handlerKey] = x =>
            {
                handler((T)x);
                return null;
            };

            return new DisposableAction(() => _handlers.Remove(handlerKey));
        }

        public IDisposable Subscribe(Subscription[] subscriptions, Action<IMessage> handler)
        {
            lock (_subscriptions)
            {
                _subscriptions.AddRange(subscriptions);
            }

            var handlerKeys = subscriptions.Select(x => new HandlerKey(x.MessageTypeId.GetMessageType(), default(PeerId))).ToList();
            foreach (var handlerKey in handlerKeys)
            {
                _handlers[handlerKey] = x =>
                {
                    handler(x);
                    return null;
                };
            }

            return new DisposableAction(() =>
            {
                _handlers.RemoveRange(handlerKeys);
                lock (_subscriptions)
                {
                    _subscriptions.RemoveRange(subscriptions);
                }
            });
        }

        public IDisposable Subscribe(Subscription subscription, Action<IMessage> handler)
        {
            lock (_subscriptions)
            {
                _subscriptions.Add(subscription);
            }

            var handlerKey = new HandlerKey(subscription.MessageTypeId.GetMessageType(), default(PeerId));
            _handlers[handlerKey] = x =>
            {
                handler(x);
                return null;
            };

            return new DisposableAction(() =>
            {
                _handlers.Remove(handlerKey);
                lock (_subscriptions)
                {
                    _subscriptions.Remove(subscription);
                }
            });
        }

        public void Reply(int errorCode)
        {
            LastReplyCode = errorCode;
        }

        public void Reply(IMessage response)
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

        public void AddHandler<TMessage>(Func<TMessage, object> handler) where TMessage : IMessage
        {
            _handlers[new HandlerKey(typeof(TMessage), default(PeerId))] = x => handler((TMessage)x);
        }

        public void AddSuccessfulHandler<TMessage>() where TMessage : IMessage
        {
            _handlers[new HandlerKey(typeof(TMessage), default(PeerId))] = x => true;
        }

        public void AddHandlerForPeer<TMessage>(PeerId peerId, Func<TMessage, object> handler) where TMessage : IMessage
        {
            _handlers[new HandlerKey(typeof(TMessage), peerId)] = x => handler((TMessage)x);
        }

        public void AddHandlerForPeer<TMessage>(PeerId peerId, Action<TMessage> handler) where TMessage : IMessage
        {
            _handlers[new HandlerKey(typeof(TMessage), peerId)] = x =>
            {
                handler((TMessage)x);
                return null;
            };
        }

        public void AddHandler<TMessage>(Action<TMessage> handler) where TMessage : IMessage
        {
            _handlers[new HandlerKey(typeof(TMessage), default(PeerId))] = x =>
            {
                handler((TMessage)x);
                return null;
            };
        }

        public void AddHandlerThatThrowsDomainException<TMessage>(DomainException ex) where TMessage : IMessage
        {
            _handlers[new HandlerKey(typeof(TMessage), default(PeerId))] = x => { throw ex; };
        }

        public void AddHandlerThatThrows<TMessage>(Exception ex = null) where TMessage : IMessage
        {
            _handlers[new HandlerKey(typeof(TMessage), default(PeerId))] = x => { throw ex ?? new Exception(); };
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

        public Peer GetRecipientPeer(ICommand command)
        {
            return _peerByCommand[command];
        }

        /// <summary>
        /// Executes handler synchronously (simulate locally handled messages).
        /// </summary>
        public class DefaultHandlerExecutor : IHandlerExecutor
        {
            public Task<CommandResult> Execute(ICommand command, Func<IMessage, object> handler)
            {
                var taskCompletionSource = new TaskCompletionSource<CommandResult>();
                try
                {
                    var result = handler != null ? handler(command) : null;
                    taskCompletionSource.SetResult(new CommandResult(0, result));
                }
                catch (DomainException ex)
                {
                    taskCompletionSource.SetResult(new CommandResult(ex.ErrorCode, null));
                }
                catch (Exception)
                {
                    taskCompletionSource.SetResult(new CommandResult(1, null));
                }
             
                return taskCompletionSource.Task;
            }
        }

        /// <summary>
        /// Executes handler asynchronously (simulate remotely handled messages).
        /// </summary>
        public class AsyncHandlerExecutor : IHandlerExecutor
        {
            public Task<CommandResult> Execute(ICommand command, Func<IMessage, object> handler)
            {
                return Task.Factory.StartNew(() =>
                {
                    var result = handler != null ? handler(command) : null;
                    return new CommandResult(0, result);
                });
            }
        }

        /// <summary>
        /// Never executes handler (simulate down peer).
        /// </summary>
        public class DoNotReplyHandlerExecutor : IHandlerExecutor
        {
            public Task<CommandResult> Execute(ICommand command, Func<IMessage, object> handler)
            {
                return new TaskCompletionSource<CommandResult>().Task;
            }
        }

        public interface IHandlerExecutor
        {
            Task<CommandResult> Execute(ICommand command, Func<IMessage, object> handler);
        }

        private class HandlerKey : Tuple<Type, PeerId>
        {
            public HandlerKey(Type type, PeerId peerId) : base(type, peerId)
            {
            }
        }

        public IDisposable InjectSubscription(Subscription subscription)
        {
            return Subscribe(subscription);
        }

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