using Abc.Zebus.Persistence.Messages;
using Abc.Zebus.Testing;
using NUnit.Framework;

namespace Abc.Zebus.Persistence.Tests
{
    [TestFixture]
    public class SerializationTests
    {
        [Test]
        public void should_serialize_messages()
        {
            MessageSerializationTester.CheckSerializationForTypesInSameAssemblyAs<PublishNonAckMessagesCountCommand>();
        }
    }
}
