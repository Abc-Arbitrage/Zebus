using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Abc.Zebus.Core;
using Abc.Zebus.DependencyInjection;
using Abc.Zebus.Routing;
using Abc.Zebus.Scan;
using Abc.Zebus.Util.Extensions;

namespace Abc.Zebus.Dispatch
{
    public abstract class MessageHandlerInvoker : IMessageHandlerInvoker
    {
        private bool? _isSingleton;
        private IBus? _bus;

        protected MessageHandlerInvoker(Type handlerType, Type messageType, bool? shouldBeSubscribedOnStartup = null)
        {
            MessageHandlerType = handlerType;
            DispatchQueueName = DispatchQueueNameScanner.GetQueueName(handlerType);
            MessageType = messageType;
            MessageTypeId = new MessageTypeId(MessageType);
            ShouldBeSubscribedOnStartup = shouldBeSubscribedOnStartup ?? MessageShouldBeSubscribedOnStartup(messageType);
        }

        public Type MessageHandlerType { get; }
        public Type MessageType { get; }
        public MessageTypeId MessageTypeId { get; }
        public bool ShouldBeSubscribedOnStartup { get; }
        public string DispatchQueueName { get; }
        public virtual MessageHandlerInvokerMode Mode => MessageHandlerInvokerMode.Synchronous;

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
            var subscriptionModeAttribute = (SubscriptionModeAttribute?)Attribute.GetCustomAttribute(handlerType, typeof(SubscriptionModeAttribute));
            if (subscriptionModeAttribute != null)
                return subscriptionModeAttribute.SubscriptionMode;

            var isNoScanHandler = Attribute.IsDefined(handlerType, typeof(NoScanAttribute));
            if (isNoScanHandler)
                return SubscriptionMode.Manual;

            return null;
        }

        protected object CreateHandler(IDependencyInjectionContainer container, MessageContext messageContext)
        {
            if (IsHandlerSingleton(container))
                return container.GetInstance(MessageHandlerType);

            _bus ??= container.GetInstance<IBus>();
            if (_bus == null)
                return container.GetInstance(MessageHandlerType);


            var busProxy = new MessageContextAwareBus(_bus, messageContext);

            return container.GetMessageHandlerInstance(MessageHandlerType, busProxy, messageContext);
        }


        private bool IsHandlerSingleton(IDependencyInjectionContainer container)
        {
            if (_isSingleton == null)
            {
                _isSingleton = container.IsSingleton(MessageHandlerType);
            }
            return _isSingleton.Value;
        }



        internal static void ThrowIfAsyncVoid(Type handlerType, MethodInfo handleMethod)
        {
            if (handleMethod.ReturnType == typeof(void) && handleMethod.GetAttribute<AsyncStateMachineAttribute>(true) != null)
                throw new InvalidProgramException($"The message handler {handlerType} has an async void Handle method. If you think there are valid use cases for this, please discuss it with the dev team");
        }
    }
}
