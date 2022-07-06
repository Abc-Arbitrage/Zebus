using System;
using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Core;

namespace Abc.Zebus.DependencyInjection
{
    public class LamarMessageHandlerContainer : IMessageHandlerContainer
    {
        private readonly bool _handlerHasMessageContext;
        private readonly LamarContainer _lamarContainer;

        public LamarMessageHandlerContainer(LamarContainer lamarContainer, Type handlerType)
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

        public bool IsSingleton(Type type) => _lamarContainer.IsSingleton(type);

        public IEnumerable<T> GetAllInstances<T>() => _lamarContainer.GetAllInstances<T>();

        public void Dispose() => _lamarContainer.Dispose();
    }
}
