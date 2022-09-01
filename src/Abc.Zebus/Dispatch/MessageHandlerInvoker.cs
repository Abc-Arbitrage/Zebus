using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Abc.Zebus.Core;
using Abc.Zebus.Scan;
using Abc.Zebus.Util.Extensions;
using StructureMap;
using StructureMap.Pipeline;

namespace Abc.Zebus.Dispatch
{
    public abstract class MessageHandlerInvoker : IMessageHandlerInvoker
    {
        private readonly IContainer _container;
        private readonly MessageHandlerInvokerSubscriber _subscriber;
        private readonly Instance _instance;
        private bool? _isSingleton;
        private IBus? _bus;

        [ThreadStatic]
        private static MessageContextAwareBus? _dispatchBus;

        protected MessageHandlerInvoker(IContainer container, Type handlerType, Type messageType, MessageHandlerInvokerSubscriber subscriber)
        {
            _container = container;
            _subscriber = subscriber;
            _instance = CreateConstructorInstance(handlerType);

            MessageHandlerType = handlerType;
            DispatchQueueName = DispatchQueueNameScanner.GetQueueName(handlerType);
            MessageType = messageType;
            MessageTypeId = new MessageTypeId(messageType);
        }

        public Type MessageHandlerType { get; }
        public Type MessageType { get; }
        public MessageTypeId MessageTypeId { get; }
        public string DispatchQueueName { get; }
        public virtual MessageHandlerInvokerMode Mode => MessageHandlerInvokerMode.Synchronous;

        public IEnumerable<Subscription> GetStartupSubscriptions()
        {
            return _subscriber.GetStartupSubscriptions(MessageType, MessageTypeId, _container);
        }

        public abstract void InvokeMessageHandler(IMessageHandlerInvocation invocation);

        public virtual Task InvokeMessageHandlerAsync(IMessageHandlerInvocation invocation)
        {
            throw new NotSupportedException("InvokeMessageHandlerAsync is not supported in Synchronous mode");
        }

        public virtual bool ShouldHandle(IMessage message)
        {
            return true;
        }

        public virtual bool CanMergeWith(IMessageHandlerInvoker other)
        {
            return false;
        }

        protected internal object CreateHandler(MessageContext messageContext)
        {
            if (IsHandlerSingleton())
                return _container.GetInstance(MessageHandlerType);

            _bus ??= _container.GetInstance<IBus>();
            if (_bus == null)
                return _container.GetInstance(MessageHandlerType);

            try
            {
                _dispatchBus = new MessageContextAwareBus(_bus, messageContext);
                return _container.GetInstance(MessageHandlerType, _instance);
            }
            finally
            {
                _dispatchBus = null;
            }
        }

        private bool IsHandlerSingleton()
        {
            if (_isSingleton == null)
            {
                var model = _container.Model?.For(MessageHandlerType);
                _isSingleton = model != null && model.Lifecycle == Lifecycles.Singleton;
            }
            return _isSingleton.Value;
        }

        private static Instance CreateConstructorInstance(Type messageHandlerType)
        {
            var inst = new ConstructorInstance(messageHandlerType);
            inst.Dependencies.Add<IBus>(new LambdaInstance<IBus>("Dispatch IBus", () => _dispatchBus!));
            inst.Dependencies.Add<MessageContext>(new LambdaInstance<MessageContext>("Dispatch MessageContext", () => _dispatchBus!.MessageContext));
            return inst;
        }

        internal static void ThrowIfAsyncVoid(Type handlerType, MethodInfo handleMethod)
        {
            if (handleMethod.ReturnType == typeof(void) && handleMethod.GetAttribute<AsyncStateMachineAttribute>(true) != null)
                throw new InvalidProgramException($"The message handler {handlerType} has an async void Handle method. If you think there are valid use cases for this, please discuss it with the dev team");
        }
    }
}
