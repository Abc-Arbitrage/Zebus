extern alias senderVersion;
using System;
using System.Collections.Generic;
using Abc.Zebus.Testing.Extensions;
using NUnit.Framework;

namespace Abc.Zebus.Tests
{
    [TestFixture]
    public class MessageUtilTests
    {
        [Test]
        public void should_detect_transcient_message()
        {
            new MessageTypeId(typeof(TranscientCommand)).IsPersistent().ShouldBeFalse();
        }

        [Test]
        public void non_transcient_messages_should_be_persistent()
        {
            new MessageTypeId(typeof(NakedMessage)).IsPersistent().ShouldBeTrue();
        }

        [Test]
        public void unknow_messages_should_be_persistent()
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
        public void unknow_messages_should_not_be_infrastructure()
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

        public class GenericEvent<T> : IEvent
        {
            
        }
        
        [Transient]
        private class TranscientCommand : ICommand
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