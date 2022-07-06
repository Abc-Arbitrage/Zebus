using System;
using System.Collections.Generic;

namespace Abc.Zebus.DependencyInjection
{
    public interface IDependencyInjectionContainer : IDisposable
    {
        object GetInstance(Type type);
        T GetInstance<T>();
        bool IsSingleton(Type type);
        IEnumerable<T> GetAllInstances<T>();
    }
}
