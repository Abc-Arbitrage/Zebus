using System;
using System.Linq;
using Abc.Zebus.Core;
using Lamar;
using Microsoft.Extensions.DependencyInjection;

namespace Abc.Zebus.DependencyInjection
{
    public class LamarContainer : IDependencyInjectionContainer
    {
        private readonly bool _handlerHasMessageContext;
        private readonly IContainer _lamarContainer;

        public LamarContainer(IContainer lamarContainer, Type handlerType)
        {
            _lamarContainer = lamarContainer;
            _handlerHasMessageContext = handlerType.GetConstructors()
                                                   .Any(x => x.GetParameters().Any(y => y.ParameterType == typeof(MessageContext) || y.ParameterType == typeof(MessageContextAwareBus)));
        }

        public object GetMessageHandlerInstance(Type type, MessageContextAwareBus dispatchBus, MessageContext messageContext)
        {
            if (_handlerHasMessageContext)
            {
                var nested = _lamarContainer.GetNestedContainer();
                nested.Inject(dispatchBus);
                nested.Inject(messageContext);
                return nested.GetInstance(type);
            }

            return _lamarContainer.GetInstance(type);
        }

        public object GetInstance(Type type) => _lamarContainer.GetInstance(type);

        public T GetInstance<T>() => _lamarContainer.GetInstance<T>();

        public bool IsSingleton(Type type)
        {
            var model = _lamarContainer.Model?.For(type);
            return model != null && model.Default.Lifetime == ServiceLifetime.Singleton;
        }
    }
}
