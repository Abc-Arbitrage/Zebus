using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Abc.Zebus.EventSourcing;
using Abc.Zebus.Serialization;
using Abc.Zebus.Testing.Comparison;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util.Extensions;
using NUnit.Framework;
using Ploeh.AutoFixture;
using Ploeh.AutoFixture.Kernel;

namespace Abc.Zebus.Testing
{
    public static class MessageSerializationTester
    {
        private static readonly MethodInfo _createAnonymousMethod = typeof(SpecimenFactory).GetMethod("CreateAnonymous", new[] { typeof(ISpecimenBuilderComposer) });
        private static readonly MethodInfo _injectMethod = typeof(FixtureRegistrar).GetMethod("Inject");
        private static readonly Serializer _serializer = new Serializer();

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
        {
            var fixture = BuildFixture();

            Inject(fixture, obj);
            CheckSerializationForType(fixture, typeof(T), obj);

            Console.WriteLine("1 message type tested");
        }

        private static Fixture BuildFixture()
        {
            var fixture = new Fixture();
            var sourcing = new DomainEventSourcing
                               {
                                   AggregateId = Guid.NewGuid(),
                                   DateTime = DateTime.Now,
                                   EventId = Guid.NewGuid(),
                                   UserId = Environment.UserName,
                                   Version = 12
                               };

            fixture.Inject(sourcing);
            fixture.Inject(new Uri(@"http://this.is.just.a.valid.url/"));
            fixture.Customize(new MultipleCustomization());
            fixture.Inject('X');
            
            return fixture;
        }

        private static void Inject(Fixture fixture, object obj)
        {
            var method = _injectMethod.MakeGenericMethod(obj.GetType());
            method.Invoke(null, new[] { fixture, obj });
        }

        private static void CheckSerializationForType(Fixture fixture, Type messageType, object message = null)
        {
            Console.Write("Testing {0} ", messageType.Name);

            if (message == null)
            {
                var genericMethod = _createAnonymousMethod.MakeGenericMethod(messageType);
                message = genericMethod.Invoke(null, new object[] { fixture });
            }

            Console.WriteLine("{{{0}}}", message);

            var bytes = _serializer.Serialize(message);
            var messageCopy = _serializer.Deserialize(messageType, bytes);

            NUnitExtensions.ShouldNotBeNull(messageCopy);

            var comparer = ComparisonExtensions.CreateComparer();
            comparer.ElementsToIgnore = new List<string> { "Item" };
            comparer.Compare(message, messageCopy);

            if (comparer.Differences.Count > 0)
                Assert.Fail(comparer.DifferencesString);
        }

        public static void DetectDuplicatedSerializationIds(params Assembly[] assemblies)
        {
            var domainEvents = assemblies.SelectMany(asm => asm.GetTypes())
                .Where(type => type.Is<IDomainEvent>())
                .Where(type => !type.IsAbstract && !type.IsInterface && !type.IsGenericTypeDefinition)
                .Select(type => new { Type = type, Attribute = type.GetAttribute<SerializationIdAttribute>(true) });


            var dic = new Dictionary<string, Type>();
            foreach (var domainEvent in domainEvents)
            {
                Console.WriteLine("Testing " + domainEvent.Type.Name + "...");
                var ns = domainEvent.Attribute != null ? domainEvent.Attribute.FullName : domainEvent.Type.FullName;
                if (dic.ContainsKey(ns))
                {
                    var sameNsType = dic[ns];
                    Assert.Fail(sameNsType.FullName + " and " + domainEvent.Type.FullName + " have the same serializationId: " + ns);
                }

                dic.Add(ns, domainEvent.Type);
            }
        }
    }
}