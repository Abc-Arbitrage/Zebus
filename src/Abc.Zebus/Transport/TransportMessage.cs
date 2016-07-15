using System;
using Abc.Zebus.Util.Annotations;
using ProtoBuf;

namespace Abc.Zebus.Transport
{
    [ProtoContract]
    public class TransportMessage
    {
        [ProtoMember(1, IsRequired = true)]
        public readonly MessageId Id;

        [ProtoMember(2, IsRequired = true)]
        public readonly MessageTypeId MessageTypeId;

        [ProtoMember(3, IsRequired = true)]
        public readonly byte[] MessageBytes;

        [ProtoMember(4, IsRequired = true)]
        public readonly OriginatorInfo Originator;

        [ProtoMember(5, IsRequired = false)]
        public string Environment { get; set; }

        [ProtoMember(6, IsRequired = false)]
        public bool? WasPersisted { get; set; }

        [ProtoIgnore]
        public DateTime ReceptionTimeUtc { get; private set; }

        public TransportMessage(MessageTypeId messageTypeId, byte[] messageBytes, Peer sender)
            : this(messageTypeId, messageBytes, sender.Id, sender.EndPoint, MessageId.NextId())
        {
        }

        public TransportMessage(MessageTypeId messageTypeId, byte[] messageBytes, PeerId senderId, string senderEndPoint, MessageId messageId)
            : this(messageTypeId, messageBytes, CreateOriginator(senderId, senderEndPoint), messageId)
        {
        }

        public TransportMessage(MessageTypeId messageTypeId, byte[] messageBytes, OriginatorInfo originator, MessageId messageId)
        {
            Id = messageId;
            MessageTypeId = messageTypeId;
            MessageBytes = messageBytes;
            Originator = originator;
        }

        [UsedImplicitly]
        private TransportMessage()
        { 
        }

        private static OriginatorInfo CreateOriginator(PeerId peerId, string peerEndPoint)
        {
            return new OriginatorInfo(peerId, peerEndPoint, MessageContext.CurrentMachineName, MessageContext.GetInitiatorUserName());
        }

        public void SetReceptionTime(DateTime receptionTimeUtc)
        {
            ReceptionTimeUtc = receptionTimeUtc;
        }

        internal static TransportMessage Infrastructure(MessageTypeId messageTypeId, PeerId peerId, string senderEndPoint)
        {
            return new TransportMessage(messageTypeId, new byte[0], peerId, senderEndPoint, MessageId.NextId());
        }
    }
}