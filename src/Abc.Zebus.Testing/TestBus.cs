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

        public TestBus()
        {
            HandlerExecutor = new DefaultHandlerExecutor();
            Subscriptions = new HashSet<Subscription>();
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
                    return _commands;
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

        public HashSet<Subscription> Subscriptions { get; private set; }
        public int LastReplyCode { get; set; }
        public object LastReplyResponse { get; set; }
        public IHandlerExecutor HandlerExecutor { get; set; }
        public bool IsStarted { get; private set; }
        public bool IsStopped { get; private set; }
        public PeerId PeerId { get; private set; }
        public bool IsRunning { get; private set; }
        public int ReceiveQueueLength { get; set; }

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
            lock (Subscriptions)
            {
                Subscriptions.Add(subscription);
            }
            return new DisposableAction(() =>
            {
                lock (Subscriptions)
                {
                    Subscriptions.Remove(subscription);
                }
            });
        }

        public IDisposable Subscribe(Subscription[] subscriptions, SubscriptionOptions options = SubscriptionOptions.Default)
        {
            lock (Subscriptions)
            {
                Subscriptions.AddRange(subscriptions);
            }
            return new DisposableAction(() =>
            {
                lock (Subscriptions)
                {
                    Subscriptions.RemoveRange(subscriptions);
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

        public IDisposable Subscribe(Type messageType, IMultiEventHandler multiEventHandler)
        {
            var handlerKey = new HandlerKey(messageType, default(PeerId));

            _handlers[handlerKey] = x =>
            {
                multiEventHandler.Handle((IEvent)x);
                return null;
            };

            return new DisposableAction(() => _handlers.Remove(handlerKey));
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
