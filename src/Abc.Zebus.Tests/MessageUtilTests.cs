extern alias senderVersion;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Abc.Zebus.Testing.Extensions;
using NUnit.Framework;

namespace Abc.Zebus.Tests
{
    [TestFixture]
    public class MessageUtilTests
    {
        [Test]
        public void should_detect_transient_message()
        {
            new MessageTypeId(typeof(TransientCommand)).IsPersistent().ShouldBeFalse();
        }

        [Test]
        public void non_transient_messages_should_be_persistent()
        {
            new MessageTypeId(typeof(NakedMessage)).IsPersistent().ShouldBeTrue();
        }

        [Test]
        public void unknown_messages_should_be_persistent()
        {
            new MessageTypeId("Abc.Unknown").IsPersistent().ShouldBeTrue();
        }

        [Test]
        public void should_detect_infrastructure_message()
        {
            new MessageTypeId(typeof(InfrastructureMessage)).IsInfrastructure().ShouldBeTrue();
        }

        [Test]
        public void non_infrastructure_messages_should_not_be_infrastructure()
        {
            new MessageTypeId(typeof(NakedMessage)).IsInfrastructure().ShouldBeFalse();
        }

        [Test]
        public void unknown_messages_should_not_be_infrastructure()
        {
            new MessageTypeId("Abc.Unknown").IsInfrastructure().ShouldBeFalse();
        }

        [Test]
        public void should_handle_normal_messages()
        {
            var messageTypeId = MessageUtil.GetTypeId(typeof(ReallyNakedMessage));

            messageTypeId.FullName.ShouldEqual("Abc.Zebus.Tests.ReallyNakedMessage");
        }

        [Test]
        public void should_handle_normal_nested_messages()
        {
            var messageTypeId = MessageUtil.GetTypeId(typeof(NakedMessage));

            messageTypeId.FullName.ShouldEqual("Abc.Zebus.Tests.MessageUtilTests+NakedMessage");
        }

        [Test]
        public void should_handle_generic_messages()
        {
            var messageTypeId = MessageUtil.GetTypeId(typeof(GenericEvent<senderVersion::VersionedLibrary.SimpleContainer>));

            messageTypeId.FullName.ShouldEqual("Abc.Zebus.Tests.MessageUtilTests+GenericEvent<VersionedLibrary.SimpleContainer>");

            messageTypeId.GetMessageType().ShouldNotBeNull();
        }

        [Test]
        public void should_resolve_generic_type()
        {
            var messageTypeId = MessageUtil.GetTypeId(typeof(GenericEvent<senderVersion::VersionedLibrary.SimpleContainer>));

            var resolvingMessageTypeId = new MessageTypeId(messageTypeId.FullName);

            resolvingMessageTypeId.GetMessageType().ShouldEqual(typeof(GenericEvent<senderVersion::VersionedLibrary.SimpleContainer>));
        }

        [Test]
        public void should_not_handle_generic_messages_with_more_than_one_generic_type()
        {
            Assert.Throws<InvalidOperationException>(() => MessageUtil.GetTypeId(typeof(GenericEvent<List<string>>)));
        }

        [Test]
        public void should_resister_message_type()
        {
            var typeA = GenerateType(true);
            var typeB = GenerateType(false);
            typeB.FullName.ShouldEqual(typeA.FullName);

            MessageUtil.RegisterMessageType(typeA);
            new MessageTypeId(typeA).IsPersistent().ShouldBeTrue();

            MessageUtil.RegisterMessageType(typeB);
            new MessageTypeId(typeB).IsPersistent().ShouldBeFalse();
        }

        [Test]
        public void should_load_message_type_id_without_cache()
        {
            var typeA = GenerateType(true);
            var typeB = GenerateType(false);
            typeB.FullName.ShouldEqual(typeA.FullName);

            // Cached
            MessageUtil.GetTypeId(typeA).IsPersistent().ShouldEqual(MessageUtil.GetTypeId(typeB).IsPersistent());

            // Uncached
            MessageUtil.GetTypeIdSkipCache(typeA).IsPersistent().ShouldBeTrue();
            MessageUtil.GetTypeIdSkipCache(typeB).IsPersistent().ShouldBeFalse();
        }

        private static Type GenerateType(bool persistent)
        {
            var moduleBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run)
                                               .DefineDynamicModule("Main");

            var typeBuilder = moduleBuilder.DefineType(
                "GeneratedMessageType",
                TypeAttributes.AutoClass | TypeAttributes.AutoLayout | TypeAttributes.BeforeFieldInit | TypeAttributes.Class | TypeAttributes.NotPublic | TypeAttributes.Sealed,
                typeof(object)
            );

            if (!persistent)
                typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(TransientAttribute).GetConstructor(Type.EmptyTypes), Array.Empty<object>()));

            return typeBuilder.CreateType();
        }

        public class GenericEvent<T> : IEvent
        {
        }

        [Transient]
        private class TransientCommand : ICommand
        {
        }

        public class NakedMessage : ICommand
        {
        }

        [Infrastructure]
        public class InfrastructureMessage : ICommand
        {
        }
    }

    public class ReallyNakedMessage : ICommand
    {
    }
}
