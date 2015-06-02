using System;
using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Util.Extensions;
using StructureMap;

namespace Abc.Zebus.Scan
{
    public abstract class MessageHandlerInvokerLoader : IMessageHandlerInvokerLoader
    {
        private readonly Type _genericHandlerType;
        private readonly Type _handlerType;

        protected MessageHandlerInvokerLoader(IContainer container, Type genericHandlerType)
        {
            _handlerType = genericHandlerType.GetInterfaces().Single();
            _genericHandlerType = genericHandlerType;

            Container = container;
        }

        protected IContainer Container { get; private set; }

        public IEnumerable<IMessageHandlerInvoker> LoadMessageHandlerInvokers(TypeSource typeSource)
        {
            foreach (var handlerType in typeSource.GetTypes())
            {
                if (!handlerType.IsClass || handlerType.IsAbstract || !handlerType.IsVisible || !_handlerType.IsAssignableFrom(handlerType))
                    continue;

                var subscriptionMode = MessageHandlerInvoker.GetExplicitSubscriptionMode(handlerType);
                var interfaces = handlerType.GetInterfaces();

                var excludedMessageTypes = interfaces.Where(IsExtendedMessageHandlerInterface)
                                                     .Select(handleInterface => handleInterface.GetGenericArguments()[0])
                                                     .ToHashSet();

                var handleInterfaces = interfaces.Where(IsMessageHandlerInterface);
                foreach (var handleInterface in handleInterfaces)
                {
                    var messageType = handleInterface.GetGenericArguments()[0];
                    if (excludedMessageTypes.Contains(messageType))
                        continue;

                    var shouldBeSubscribedOnStartup = MessageHandlerInvoker.MessageShouldBeSubscribedOnStartup(messageType, subscriptionMode);
                    var invoker = BuildMessageHandlerInvoker(handlerType, messageType, shouldBeSubscribedOnStartup);
                    yield return invoker;
                }
            }
        }

        protected abstract IMessageHandlerInvoker BuildMessageHandlerInvoker(Type handlerType, Type messageType, bool shouldBeSubscribedOnStartup);

        private bool IsExtendedMessageHandlerInterface(Type interfaceType)
        {
            return interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IExtendedMessageHandler<>) && !interfaceType.GetGenericArguments()[0].IsGenericParameter;
        }

        private bool IsMessageHandlerInterface(Type interfaceType)
        {
            return interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == _genericHandlerType && !interfaceType.GetGenericArguments()[0].IsGenericParameter;
        }
    }
}