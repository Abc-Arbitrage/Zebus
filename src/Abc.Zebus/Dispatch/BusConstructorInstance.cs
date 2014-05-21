using System;
using StructureMap;
using StructureMap.Pipeline;

namespace Abc.Zebus.Dispatch
{
    internal class BusConstructorInstance : ConstructorInstance, IConfiguredInstance
    {
        private readonly IBus _bus;

        public BusConstructorInstance(Type pluggedType, IBus bus) : base(pluggedType)
        {
            _bus = bus;
        }

        object IConfiguredInstance.Get(string propertyName, Type pluginType, BuildSession session)
        {
            // changed from ConstructorInstance

            if (pluginType == typeof(IBus))
                return _bus;

            return new DefaultInstance().Build(pluginType, session);
        }

        T IConfiguredInstance.Get<T>(string propertyName, BuildSession session)
        {
            // changed from ConstructorInstance

            if (typeof(T) == typeof(IBus))
                return (T)_bus;

            return (T)new DefaultInstance().Build(typeof(T), session);
        }
    }
}