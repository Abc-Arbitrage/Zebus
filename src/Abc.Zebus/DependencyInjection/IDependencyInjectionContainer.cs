using System;
using System.Collections.Generic;

namespace Abc.Zebus.DependencyInjection
{
    public interface IDependencyInjectionContainer : IDisposable
    {
        object GetInstance(Type type);
        object TryGetInstance(Type type);

        T GetInstance<T>();
        T TryGetInstance<T>();

        bool IsSingleton(Type type);
        IEnumerable<T> GetAllInstances<T>();
    }
}
