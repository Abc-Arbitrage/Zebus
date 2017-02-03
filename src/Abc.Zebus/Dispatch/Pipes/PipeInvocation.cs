using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abc.Zebus.Core;
using Abc.Zebus.Util;
using Abc.Zebus.Util.Extensions;

namespace Abc.Zebus.Dispatch.Pipes
{
    public class PipeInvocation : IMessageHandlerInvocation
    {
        private static readonly BusMessageLogger _messageLogger = new BusMessageLogger(typeof(PipeInvocation), "Abc.Zebus.Dispatch");
        private readonly List<Action<object>> _handlerMutations = new List<Action<object>>();
        private readonly IMessageHandlerInvoker _invoker;
        private readonly IList<IMessage> _messages;
        private readonly MessageContext _messageContext;
        private readonly IList<IPipe> _pipes;

        public PipeInvocation(IMessageHandlerInvoker invoker, List<IMessage> messages, MessageContext messageContext, IEnumerable<IPipe> pipes)
        {
            _invoker = invoker;
            _messages = messages;
            _messageContext = messageContext;
            _pipes = pipes.AsList();
        }

        internal IList<IPipe> Pipes => _pipes;

        public IMessageHandlerInvoker Invoker => _invoker;

        public IList<IMessage> Messages => _messages;

        public MessageContext Context => _messageContext;

        public void AddHandlerMutation(Action<object> action)
        {
            _handlerMutations.Add(action);
        }

        protected internal virtual void Run()
        {
            var pipeStates = BeforeInvoke();

            try
            {
                _invoker.InvokeMessageHandler(this);
            }
            catch (Exception exception)
            {
                AfterInvoke(pipeStates, true, exception);
                throw;
            }

            AfterInvoke(pipeStates, false, null);
        }

        private object[] BeforeInvoke()
        {
            var stateRef = new BeforeInvokeArgs.StateRef();
            var pipeStates = new object[_pipes.Count];
            for (var pipeIndex = 0; pipeIndex < _pipes.Count; ++pipeIndex)
            {
                var beforeInvokeArgs = new BeforeInvokeArgs(this, stateRef);
                _pipes[pipeIndex].BeforeInvoke(beforeInvokeArgs);
                pipeStates[pipeIndex] = beforeInvokeArgs.State;
            }
            return pipeStates;
        }

        private void AfterInvoke(object[] pipeStates, bool isFaulted, Exception exception)
        {
            for (var pipeIndex = _pipes.Count - 1; pipeIndex >= 0; --pipeIndex)
            {
                var afterInvokeArgs = new AfterInvokeArgs(this, pipeStates[pipeIndex], isFaulted, exception);
                _pipes[pipeIndex].AfterInvoke(afterInvokeArgs);
            }
        }

        protected internal virtual Task RunAsync()
        {
            var pipeStates = BeforeInvoke();

            var runTask = _invoker.InvokeMessageHandlerAsync(this);

            if (runTask.Status == TaskStatus.Created)
            {
                var exception = new InvalidProgramException($"{Invoker.MessageHandlerType.Name}.Handle({Invoker.MessageType.Name}) did not start the returned task");
                runTask = TaskUtil.FromError(exception);
            }

            runTask.ContinueWith(task => AfterInvoke(pipeStates, task.IsFaulted, task.Exception), TaskContinuationOptions.ExecuteSynchronously);

            return runTask;
        }

        IDisposable IMessageHandlerInvocation.SetupForInvocation()
        {
            _messageLogger.InfoFormat("HANDLE: {0} [{1}]", _messages[0], _messageContext.MessageId);

            return MessageContext.SetCurrent(_messageContext);
        }

        IDisposable IMessageHandlerInvocation.SetupForInvocation(object messageHandler)
        {
            _messageLogger.InfoFormat("HANDLE: {0} [{1}]", _messages[0], _messageContext.MessageId);

            ApplyMutations(messageHandler);

            return MessageContext.SetCurrent(_messageContext);
        }

        private void ApplyMutations(object messageHandler)
        {
            var messageContextAwareHandler = messageHandler as IMessageContextAware;
            if (messageContextAwareHandler != null)
                messageContextAwareHandler.Context = Context;

            foreach (var messageHandlerMutation in _handlerMutations)
            {
                messageHandlerMutation(messageHandler);
            }
        }
    }
}
