using ProtoBuf;

namespace Abc.Zebus.Tests.Transport.V1_5_0
{
    [ProtoContract]
    public class TransportMessage_1_5_0
    {
        [ProtoMember(1, IsRequired = true)]
        public MessageId Id;

        [ProtoMember(2, IsRequired = true)]
        public MessageTypeId MessageTypeId;

        [ProtoMember(3, IsRequired = true)]
        public byte[] Content;

        [ProtoMember(4, IsRequired = true)]
        public OriginatorInfo_1_5_0 Originator;

        [ProtoMember(5, IsRequired = false)]
        public string Environment { get; set; }

        [ProtoMember(6, IsRequired = false)]
        public bool? WasPersisted { get; set; }

    }
}