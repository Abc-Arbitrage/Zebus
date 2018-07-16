using System;
using Moq;
using StructureMap;
using StructureMap.Graph;
using StructureMap.Pipeline;

namespace Abc.Zebus.Persistence.Tests.TestUtil
{
    public class MockContainer : Container
    {
        public void Register<T>(T instance) where T : class
        {
            Configure(x => x.For<T>().Use(instance));
        }

        public Mock<T> GetMock<T>() where T : class
        {
            var existingMock = TryGetInstance<Mock<T>>();
            if (existingMock != null)
                return existingMock;

            var newMock = new Mock<T>();
            Configure(x =>
            {
                x.For<T>().Use(newMock.Object);
                x.For<Mock<T>>().Use(newMock);
            });

            return newMock;
        }

        public void FillMissingParameterTypesWithMocks<T>()
        {
            Configure(x =>
            {
                var constructorInfo = new GreediestConstructorSelector().Find(typeof(T), new DependencyCollection(), PluginGraph.CreateRoot());

                foreach (var parameter in constructorInfo.GetParameters())
                {
                    if (TryGetInstance(parameter.ParameterType) != null)
                        continue;

                    var parameterMock = CreateMockFromType(parameter.ParameterType);

                    x.For(parameter.ParameterType).Use(parameterMock.Object);
                    x.For(parameterMock.GetType()).Use(parameterMock);
                }
            });
        }

        private Mock CreateMockFromType(Type parameterType)
        {
            var closedMockType = typeof(Mock<>).MakeGenericType(parameterType);
            return (Mock)Activator.CreateInstance(closedMockType);
        }
    }
}
