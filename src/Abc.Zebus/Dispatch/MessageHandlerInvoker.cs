using System;
using System.Threading.Tasks;
using Abc.Zebus.Core;
using Abc.Zebus.Routing;
using Abc.Zebus.Scan;
using StructureMap;
using StructureMap.Pipeline;

namespace Abc.Zebus.Dispatch
{
    public abstract class MessageHandlerInvoker : IMessageHandlerInvoker
    {
        private readonly Instance _instance;
        private bool? _isSingleton;
        private IBus _bus;
        private MessageContextAwareBus _dispatchBus;

        protected MessageHandlerInvoker(Type handlerType, Type messageType, bool? shouldBeSubscribedOnStartup = null)
        {
            MessageHandlerType = handlerType;
            DispatchQueueName = DispatchQueueNameScanner.GetQueueName(handlerType);
            MessageType = messageType;
            MessageTypeId = new MessageTypeId(MessageType);
            ShouldBeSubscribedOnStartup = shouldBeSubscribedOnStartup ?? MessageShouldBeSubscribedOnStartup(messageType);

            _instance = CreateConstructorInstance(handlerType);
        }

        public Type MessageHandlerType { get; private set; }
        public Type MessageType { get; private set; }
        public MessageTypeId MessageTypeId { get; private set; }
        public bool ShouldBeSubscribedOnStartup { get; private set; }
        public string DispatchQueueName { get; private set; }

        public virtual bool ShouldCreateStartedTasks
        {
            get { return false; }
        }

        public virtual bool CanInvokeSynchronously
        {
            get { return true; }
        }

        public abstract void InvokeMessageHandler(IMessageHandlerInvocation invocation);

        public virtual Task InvokeMessageHandlerAsync(IMessageHandlerInvocation invocation)
        {
            return new Task(() => InvokeMessageHandler(invocation), TaskCreationOptions.HideScheduler);
        }

        public static bool MessageShouldBeSubscribedOnStartup(Type messageType, Type handlerType)
        {
            return MessageShouldBeSubscribedOnStartup(messageType, GetExplicitSubscriptionMode(handlerType));
        }

        internal static bool MessageShouldBeSubscribedOnStartup(Type messageType, SubscriptionMode? subscriptionMode = null)
        {
            if (subscriptionMode != null)
                return subscriptionMode == SubscriptionMode.Auto;

            return !Attribute.IsDefined(messageType, typeof(Routable));
        }

        internal static SubscriptionMode? GetExplicitSubscriptionMode(Type handlerType)
        {
            var subscriptionModeAttribute = (SubscriptionModeAttribute)Attribute.GetCustomAttribute(handlerType, typeof(SubscriptionModeAttribute));
            if (subscriptionModeAttribute != null)
                return subscriptionModeAttribute.SubscriptionMode;

            var isNoScanHandler = Attribute.IsDefined(handlerType, typeof(NoScanAttribute));
            if (isNoScanHandler)
                return SubscriptionMode.Manual;

            return null;
        }

        protected object CreateHandler(IContainer container, MessageContext messageContext)
        {
            if (IsHandlerSingleton(container))
                return container.GetInstance(MessageHandlerType);

            _bus = _bus ?? container.GetInstance<IBus>();
            if (_bus == null)
                return container.GetInstance(MessageHandlerType);

            try
            {
                _dispatchBus = new MessageContextAwareBus(_bus, messageContext);
                return container.GetInstance(MessageHandlerType, _instance);
            }
            finally
            {
                _dispatchBus = null;
            }
        }

        private bool IsHandlerSingleton(IContainer container)
        {
            if (_isSingleton == null)
            {
                var model = container.Model != null ? container.Model.For(MessageHandlerType) : null;
                _isSingleton = model != null && model.Lifecycle == Lifecycles.Singleton;
            }
            return _isSingleton.Value;
        }

        private Instance CreateConstructorInstance(Type messageHandlerType)
        {
            var inst = new ConstructorInstance(messageHandlerType);
            inst.Dependencies.Add<IBus>(new LambdaInstance<IBus>("Dispatch IBus", () => _dispatchBus));
            inst.Dependencies.Add<MessageContext>(new LambdaInstance<MessageContext>("Dispatch MessageContext", () => _dispatchBus.MessageContext));
            return inst;
        }
    }
}
