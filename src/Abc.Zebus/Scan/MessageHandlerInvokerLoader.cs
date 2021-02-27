using System;
using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.DependencyInjection;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Util.Extensions;

namespace Abc.Zebus.Scan
{
    public abstract class MessageHandlerInvokerLoader : IMessageHandlerInvokerLoader
    {
        private readonly Type _genericHandlerType;
        private readonly Type _handlerType;

        protected MessageHandlerInvokerLoader(IDependencyInjectionContainerProvider containerProvider, Type genericHandlerType)
        {
            _handlerType = genericHandlerType.GetInterfaces().Single();
            _genericHandlerType = genericHandlerType;

            ContainerProvider = containerProvider;
        }

        protected IDependencyInjectionContainerProvider ContainerProvider { get; }

        public IEnumerable<IMessageHandlerInvoker> LoadMessageHandlerInvokers(TypeSource typeSource)
        {
            foreach (var handlerType in typeSource.GetTypes())
            {
                if (!handlerType.IsClass || handlerType.IsAbstract || !handlerType.IsVisible || !_handlerType.IsAssignableFrom(handlerType))
                    continue;

                var subscriptionMode = MessageHandlerInvoker.GetExplicitSubscriptionMode(handlerType);
                var interfaces = handlerType.GetInterfaces();

                var excludedMessageTypes = interfaces.Where(IsCustomInvokerMessageHandlerInterface)
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

        private static bool IsCustomInvokerMessageHandlerInterface(Type interfaceType)
        {
            return interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(ICustomInvokerMessageHandler<>) && !interfaceType.GetGenericArguments()[0].IsGenericParameter;
        }

        private bool IsMessageHandlerInterface(Type interfaceType)
        {
            return interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == _genericHandlerType && !interfaceType.GetGenericArguments()[0].IsGenericParameter;
        }
    }
}
