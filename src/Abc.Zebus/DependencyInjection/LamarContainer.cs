using System;
using Abc.Zebus.Core;
using Lamar;
using Microsoft.Extensions.DependencyInjection;

namespace Abc.Zebus.DependencyInjection
{
    public class LamarContainer : IDependencyInjectionContainer
    {
        private readonly IContainer _lamarContainer;

        public LamarContainer(IContainer lamarContainer)
        {
            _lamarContainer = lamarContainer;
        }

        public object GetMessageHandlerInstance(Type type, MessageContextAwareBus dispatchBus, MessageContext messageContext)
        {
            var nested = _lamarContainer.GetNestedContainer();
            nested.Inject(dispatchBus);
            nested.Inject(messageContext);
            return nested.GetInstance(type);
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
