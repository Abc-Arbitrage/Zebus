using System;
using System.Collections.Concurrent;
using Abc.Zebus.Snapshotting;
using StructureMap;

namespace Abc.Zebus.Scan
{
    /*public interface ISnapshotGeneratorLoader
    {
    }

    public class SnapshotGeneratorLoader : ISnapshotGeneratorLoader
    {
        private readonly IContainer _container;
        private readonly ConcurrentDictionary<Type, Type> _snapshotGenerators = new ConcurrentDictionary<Type, Type>();

        public SnapshotGeneratorLoader(IContainer container)
        {
            _container = container;
        }

        public void LoadSnapshotGenerators(TypeSource typeSource)
        {
            foreach (var handlerType in typeSource.GetTypes())
            {
                if (!handlerType.IsClass || handlerType.IsAbstract || !handlerType.IsVisible || !typeof(ISnapshotGenerator).IsAssignableFrom(handlerType))
                    continue;

                var argument = handlerType.GenericTypeArguments[0];
                _snapshotGenerators.TryAdd(argument, handlerType);
            }
        }

        public ISnapshotGenerator<T> GetSnapshotGenerator<T>()
            where T : IEvent
        {
            if (_snapshotGenerators.TryGetValue(typeof(T), out var generatorType))
            {
                var instance = _container.GetInstance(generatorType);
                return (ISnapshotGenerator<T>)instance;
            }

            return null;
        }
    }*/
}
