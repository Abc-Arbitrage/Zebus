using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Abc.Zebus.Serialization;
using Abc.Zebus.Testing.Comparison;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util.Extensions;
using AutoFixture;
using AutoFixture.Kernel;
using NUnit.Framework;

namespace Abc.Zebus.Testing
{
    public static class MessageSerializationTester
    {
        private static readonly MethodInfo _createMethod = typeof(SpecimenFactory).GetMethod("Create", new[] { typeof(ISpecimenBuilder) });
        private static readonly MethodInfo _injectMethod = typeof(FixtureRegistrar).GetMethod("Inject");

        public static void CheckSerializationForTypesInSameAssemblyAs<T>(params object[] prebuiltObjects)
        {
            var fixture = BuildFixture();

            var messageTypes = typeof(T).Assembly.GetTypes()
                                        .Where(type => (type.Is<IMessage>() || type.GetInterfaces().Any(x => x.Name == "ISnapshot") || type.GetInterfaces().Any(x => x.Name == "IZmqMessage")))
                                        .Where(type => !type.IsAbstract && !type.IsInterface && !type.IsGenericTypeDefinition);

            var prebuildObjectsTypes = prebuiltObjects.Select(x => x.GetType()).ToList();
            var typesToInstanciate = messageTypes.Where(msgType => !prebuildObjectsTypes.Contains(msgType)).ToList();

            foreach (var prebuiltObject in prebuiltObjects)
            {
                Inject(fixture, prebuiltObject);
            }

            foreach (var messageType in typesToInstanciate)
                CheckSerializationForType(fixture, messageType);

            foreach (var obj in prebuiltObjects)
                CheckSerializationForType(fixture, obj.GetType(), obj);

            var count = typesToInstanciate.Count + prebuiltObjects.Length;
            Console.WriteLine("{0} message types tested", count);
        }

        public static void CheckSerializationFor<T>(T obj)
            where T : notnull
        {
            var fixture = BuildFixture();

            Inject(fixture, obj);
            CheckSerializationForType(fixture, typeof(T), obj);

            Console.WriteLine("1 message type tested");
        }

        private static Fixture BuildFixture()
        {
            var fixture = new Fixture();

            fixture.Inject(new Uri(@"http://this.is.just.a.valid.url/"));
            fixture.Inject('X');

            return fixture;
        }

        private static void Inject(Fixture fixture, object obj)
        {
            var method = _injectMethod.MakeGenericMethod(obj.GetType());
            method.Invoke(null, new[] { fixture, obj });
        }

        private static void CheckSerializationForType(Fixture fixture, Type messageType, object? message = null)
        {
            Console.Write("Testing {0} ", messageType.Name);

            if (message == null)
            {
                var genericMethod = _createMethod.MakeGenericMethod(messageType);
                message = genericMethod.Invoke(null, new object[] { fixture });
            }

            Console.WriteLine("{{{0}}}", message);

            var bytes = Serializer.Serialize(message);
            var messageCopy = Serializer.Deserialize(messageType, bytes);

            messageCopy.ShouldNotBeNull();

            var comparer = ComparisonExtensions.CreateComparer();
            comparer.Config.MembersToIgnore = new List<string> { "Item" };
            var result = comparer.Compare(message, messageCopy);

            if (!result.AreEqual)
                Assert.Fail(result.DifferencesString);
        }
    }
}
