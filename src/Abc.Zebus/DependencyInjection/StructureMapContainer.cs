﻿using System;
using System.Collections.Generic;
using Abc.Zebus.Core;
using StructureMap;
using StructureMap.Pipeline;

namespace Abc.Zebus.DependencyInjection
{
    public class StructureMapContainer : IMessageHandlerContainer
    {
        private readonly IContainer _structureMapContainer;

        public StructureMapContainer(IContainer structureMapContainer)
        {
            _structureMapContainer = structureMapContainer;
        }

        public object GetMessageHandlerInstance(Type type, MessageContextAwareBus busProxy, MessageContext messageContext)
        {
            var explicitArgs = new ExplicitArguments()
                               .Set(typeof(IBus), busProxy)
                               .Set(typeof(MessageContext), messageContext);

            return _structureMapContainer.GetInstance(type, explicitArgs);
        }

        public object GetInstance(Type type) => _structureMapContainer.GetInstance(type);

        public object TryGetInstance(Type type) => _structureMapContainer.TryGetInstance(type);

        public T GetInstance<T>() => _structureMapContainer.GetInstance<T>();

        public T TryGetInstance<T>() => _structureMapContainer.TryGetInstance<T>();

        public bool IsSingleton(Type type)
        {
            var model = _structureMapContainer.Model?.For(type);
            return model != null && model.Lifecycle == Lifecycles.Singleton;
        }

        public IEnumerable<T> GetAllInstances<T>() => _structureMapContainer.GetAllInstances<T>();

        public void Dispose() => _structureMapContainer.Dispose();
    }
}
