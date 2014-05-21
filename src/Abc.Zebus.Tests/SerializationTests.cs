using System;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Transport;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Abc.Zebus.Tests
{
    [TestFixture]
    public class SerializationTests
    {
        [Test]
        public void should_serialize_messages()
        {
            var prebuildObjectTypes = new object[]
            {
                new MessageTypeId("X"),
                MessageId.NextId(),
                new TransportMessage(new MessageTypeId("lol"), new byte[] { 1, 2, 3}, new PeerId("peer"), "endpoint", MessageId.NextId()), 
            };

            SimpleSerializationTestHelper.CheckSerializationForTypesInSameAssemblyAs<IBus>(prebuildObjectTypes);
        }

        [Test]
        public void should_detect_duplicate_serialization_ids()
        {
            SimpleSerializationTestHelper.DetectDuplicatedSerializationIds(typeof(IBus).Assembly);
        }

        [Test]
        public void should_convert_peer_id_to_json_string()
        {
            var peerId = new PeerId("Abc.Testing.42");
            var text = JsonConvert.SerializeObject(new MessageWithPeerId { PeerId1 =  peerId });

            text.ShouldContain(peerId.ToString());

            Console.WriteLine(text);

            var message = JsonConvert.DeserializeObject<MessageWithPeerId>(text);
            message.PeerId1.ShouldEqual(peerId);
            message.PeerId2.ShouldBeNull();
        }

        private class MessageWithPeerId
        {
            public PeerId PeerId1;
            public PeerId? PeerId2 = null;
        }
    }
}