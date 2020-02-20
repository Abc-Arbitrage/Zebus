using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Util.Collections;
using log4net;

namespace Abc.Zebus.Dispatch.Pipes
{
    internal class PipeManager : IPipeManager
    {
        private static readonly ILog _logger = LogManager.GetLogger(typeof(PipeManager));

        private readonly ConcurrentDictionary<Type, PipeList> _pipesByMessageType = new ConcurrentDictionary<Type, PipeList>();
        private readonly ConcurrentSet<string> _enabledPipeNames = new ConcurrentSet<string>();
        private readonly ConcurrentSet<string> _disabledPipeNames = new ConcurrentSet<string>();
        private readonly Func<Type, PipeList> _createPipeList;
        private readonly IPipeSource[] _pipeSources;

        public PipeManager(IPipeSource[] pipeSources)
        {
            _pipeSources = pipeSources;
            _createPipeList = CreatePipeList;
        }

        public void EnablePipe(string pipeName)
        {
            _logger.InfoFormat("Enabling pipe [{0}]", pipeName);

            _enabledPipeNames.Add(pipeName);
            _disabledPipeNames.Remove(pipeName);

            foreach (var pipeListEntry in _pipesByMessageType.Values)
                pipeListEntry.ReloadEnabledPipes();
        }

        public void DisablePipe(string pipeName)
        {
            _logger.InfoFormat("Disabling pipe [{0}]", pipeName);

            _enabledPipeNames.Remove(pipeName);
            _disabledPipeNames.Add(pipeName);

            foreach (var pipeListEntry in _pipesByMessageType.Values)
                pipeListEntry.ReloadEnabledPipes();
        }

        public PipeInvocation BuildPipeInvocation(IMessageHandlerInvoker messageHandlerInvoker, List<IMessage> messages, MessageContext messageContext)
        {
            var pipes = GetEnabledPipes(messageHandlerInvoker.MessageHandlerType);
            return new PipeInvocation(messageHandlerInvoker, messages, messageContext, pipes);
        }

        public IEnumerable<IPipe> GetEnabledPipes(Type messageHandlerType)
            => GetPipeList(messageHandlerType).EnabledPipes;

        private PipeList GetPipeList(Type messageHandlerType)
            => _pipesByMessageType.GetOrAdd(messageHandlerType, _createPipeList);

        private PipeList CreatePipeList(Type handlerType)
            => new PipeList(this, _pipeSources.SelectMany(x => x.GetPipes(handlerType)));

        private bool IsPipeEnabled(IPipe pipe)
            => !_disabledPipeNames.Contains(pipe.Name)
               && (pipe.IsAutoEnabled || _enabledPipeNames.Contains(pipe.Name));

        private class PipeList
        {
            private readonly PipeManager _pipeManager;
            private readonly List<IPipe> _pipes;

            public PipeList(PipeManager pipeManager, IEnumerable<IPipe> pipes)
            {
                _pipeManager = pipeManager;
                _pipes = pipes.OrderByDescending(x => x.Priority).ToList();

                ReloadEnabledPipes();
            }

            public IList<IPipe> EnabledPipes { get; private set; } = default!;

            internal void ReloadEnabledPipes()
            {
                EnabledPipes = _pipes.Where(_pipeManager.IsPipeEnabled).ToList();
            }
        }
    }
}
