using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ABC.ServiceBus;
using Abc.Shared.Extensions;
using Abc.Shared.Tpl;
using Abc.Zebus.Dispatch;

namespace Abc.Zebus.Pipes
{
    public class PipeInvocation : IMessageHandlerInvocation
    {
        private readonly List<Action<object>> _handlerMutations = new List<Action<object>>();
        private readonly IMessageHandlerInvoker _invoker;
        private readonly IMessage _message;
        private readonly MessageContext _messageContext;
        private readonly IList<IPipe> _pipes;

        public PipeInvocation(IMessageHandlerInvoker invoker, IMessage message, MessageContext messageContext, IEnumerable<IPipe> pipes)
        {
            _invoker = invoker;
            _message = message;
            _messageContext = messageContext;
            _pipes = pipes.AsList();
        }

        internal IList<IPipe> Pipes
        {
            get { return _pipes; }
        }

        public IMessageHandlerInvoker Invoker
        {
            get { return _invoker; }
        }

        public IMessage Message
        {
            get { return _message; }
        }

        public MessageContext Context
        {
            get { return _messageContext; }
        }

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
            object[] pipeStates;

            try
            {
                pipeStates = BeforeInvoke();
            }
            catch (Exception ex)
            {
                return TaskHelper.FromError(ex);
            }

            var runTask = _invoker.InvokeMessageHandlerAsync(this);
            runTask.ContinueWith(task => AfterInvoke(pipeStates, task.IsFaulted, task.Exception), TaskContinuationOptions.ExecuteSynchronously);

            return runTask;
        }

        IDisposable IMessageHandlerInvocation.ApplyContext()
        {
            return MessageContext.SetCurrent(_messageContext);
        }

        IDisposable IMessageHandlerInvocation.ApplyContext(object messageHandler)
        {
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
