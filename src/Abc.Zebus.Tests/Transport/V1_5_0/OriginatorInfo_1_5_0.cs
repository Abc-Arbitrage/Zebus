using ProtoBuf;

namespace Abc.Zebus.Tests.Transport.V1_5_0
{
    [ProtoContract]
    public class OriginatorInfo_1_5_0
    {
        [ProtoMember(1, IsRequired = true)]
        public PeerId SenderId;

        [ProtoMember(2, IsRequired = true)]
        public string SenderEndPoint;

        [ProtoMember(3, IsRequired = true)]
        public string SenderMachineName;

        [ProtoMember(5, IsRequired = true)]
        public string InitiatorUserName;
    }
}