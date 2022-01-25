using System;
using JetBrains.Annotations;
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
        internal readonly string? SenderMachineName;

        [ProtoMember(5, IsRequired = true)]
        public string? InitiatorUserName;

        public OriginatorInfo(PeerId senderId, string senderEndPoint, string? senderMachineName, string? initiatorUserName)
        {
            SenderId = senderId;
            SenderEndPoint = senderEndPoint;
            SenderMachineName = senderMachineName;
            InitiatorUserName = initiatorUserName;
        }

        [UsedImplicitly]
        internal OriginatorInfo()
        {
            SenderEndPoint = default!;
        }

        public string GetSenderHostNameFromEndPoint()
            => new Uri(SenderEndPoint).Host;

        public string GetSenderMachineNameFromEndPoint()
        {
            var host = GetSenderHostNameFromEndPoint();
            var separatorIndex = host.IndexOf('.');
            return separatorIndex >= 0 ? host.Substring(0, separatorIndex) : host;
        }
    }
}
