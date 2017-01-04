using System;
using System.IO;
using Abc.Zebus.Serialization.Protobuf;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Messages;
using Abc.Zebus.Transport;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Transport
{
    [TestFixture]
    public class TransportMessageReaderTests
    {
        [Test]
        public void should_read_message()
        {
            var transportMessage = TestDataBuilder.CreateTransportMessage<FakeCommand>();

            var outputStream = new CodedOutputStream();
            TransportMessageWriter.Write(outputStream, transportMessage);

            var inputStream = new CodedInputStream(outputStream.Buffer, 0, outputStream.Position);
            var deserialized = TransportMessageReader.Read(inputStream);

            deserialized.Id.ShouldEqual(transportMessage.Id);
            deserialized.MessageTypeId.ShouldEqual(transportMessage.MessageTypeId);
            deserialized.GetContentBytes().ShouldEqual(transportMessage.GetContentBytes());
            deserialized.Originator.ShouldEqualDeeply(transportMessage.Originator);
            deserialized.Environment.ShouldEqual(transportMessage.Environment);
            deserialized.WasPersisted.ShouldEqual(transportMessage.WasPersisted);
        }
    }
}