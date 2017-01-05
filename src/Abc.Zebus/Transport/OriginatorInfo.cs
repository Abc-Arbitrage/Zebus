using System;
using Abc.Zebus.Util.Annotations;
using ProtoBuf;

namespace Abc.Zebus.Transport
{
    [ProtoContract]
    public class OriginatorInfo
    {
        [ProtoMember(1, IsRequired = true)]
        public readonly PeerId SenderId;

        [ProtoMember(2, IsRequired = true)]
        public readonly string SenderEndPoint;

        [ProtoMember(3, IsRequired = true)]
        internal readonly string SenderMachineName;

        [ProtoMember(5, IsRequired = true)]
        public string InitiatorUserName;

        public OriginatorInfo(PeerId senderId, string senderEndPoint, string senderMachineName, string initiatorUserName)
        {
            SenderId = senderId;
            SenderEndPoint = senderEndPoint;
            SenderMachineName = senderMachineName;
            InitiatorUserName = initiatorUserName;
        }

        [UsedImplicitly]
        private OriginatorInfo()
        {
        }

        public string GetSenderMachineNameFromEndPoint()
        {
            var uri = new Uri(SenderEndPoint);
            return uri.Host;
        }
    }
}