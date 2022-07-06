using System;
using System.Collections.Generic;
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

        public object GetInstance(Type type) => _lamarContainer.GetInstance(type);

        public object TryGetInstance(Type type) => _lamarContainer.TryGetInstance(type);

        public T GetInstance<T>() => _lamarContainer.GetInstance<T>();

        public T TryGetInstance<T>() => _lamarContainer.TryGetInstance<T>();

        public bool IsSingleton(Type type)
        {
            var model = _lamarContainer.Model?.For(type);
            return model != null && model.Default.Lifetime == ServiceLifetime.Singleton;
        }

        public IEnumerable<T> GetAllInstances<T>() => _lamarContainer.GetAllInstances<T>();

        public void Dispose() => _lamarContainer.Dispose();

        internal INestedContainer GetNestedContainer() => _lamarContainer.GetNestedContainer();
    }
}
