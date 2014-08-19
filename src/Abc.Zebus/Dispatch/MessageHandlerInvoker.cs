using System;
using System.Threading.Tasks;
using Abc.Zebus.Core;
using Abc.Zebus.Routing;
using Abc.Zebus.Scan;
using StructureMap;

namespace Abc.Zebus.Dispatch
{
    public abstract class MessageHandlerInvoker : IMessageHandlerInvoker
    {
        private bool? _isSingleton;
        private IBus _bus;

        protected MessageHandlerInvoker(Type handlerType, Type messageType, bool? shouldBeSubscribedOnStartup = null)
        {
            MessageHandlerType = handlerType;
            DispatchQueueName = DispatchQueueNameScanner.GetQueueName(handlerType);
            MessageType = messageType;
            MessageTypeId = new MessageTypeId(MessageType);
            ShouldBeSubscribedOnStartup = shouldBeSubscribedOnStartup ?? MessageShouldBeSubscribedOnStartup(messageType);
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

        protected internal static bool MessageShouldBeSubscribedOnStartup(Type messageType, bool isNoScanHandler = false)
        {
            return !isNoScanHandler && !Attribute.IsDefined(messageType, typeof(Routable));
        }

        protected object CreateHandler(IContainer container, MessageContext messageContext)
        {
            if (IsHandlerSingleton(container))
                return container.GetInstance(MessageHandlerType);

            _bus = _bus ?? container.GetInstance<IBus>();
            if (_bus == null)
                return container.GetInstance(MessageHandlerType);

            var busProxy = new MessageContextAwareBus(_bus, messageContext);
            var messageHandlerInstance = new MessageHandlerConstructorInstance(MessageHandlerType, busProxy, messageContext);

            return container.GetInstance(MessageHandlerType, messageHandlerInstance);
        }

        private bool IsHandlerSingleton(IContainer container)
        {
            if (_isSingleton == null)
            {
                var model = container.Model != null ? container.Model.For(MessageHandlerType) : null;
                _isSingleton = model != null && model.Lifecycle == "Singleton";
            }
            return _isSingleton.Value;
        }
    }
}