using System.Collections.Generic;
using System.IO;
using Abc.Zebus.Util;
using Abc.Zebus.Util.Annotations;
using Newtonsoft.Json;
using ProtoBuf;

namespace Abc.Zebus.Transport
{
    [ProtoContract]
    public class TransportMessage
    {
        [ProtoMember(1, IsRequired = true)]
        public MessageId Id { get; internal set; }

        [ProtoMember(2, IsRequired = true)]
        public MessageTypeId MessageTypeId { get; internal set; }

        [ProtoMember(3, IsRequired = true)]
        private byte[] ContentBytes
        {
            get { return GetContentBytes(); }
            set { Content = new MemoryStream(value); }
        }

        [ProtoIgnore, JsonIgnore]
        public Stream Content { get; internal set; }

        [ProtoMember(4, IsRequired = true)]
        public OriginatorInfo Originator { get; internal set; }

        [ProtoMember(5, IsRequired = false)]
        public string Environment { get; internal set; }

        [ProtoMember(6, IsRequired = false)]
        public bool? WasPersisted { get; internal set; }

        [ProtoMember(7, IsRequired = false)]
        public List<PeerId> PersistentPeerIds { get; set; }

        [JsonIgnore]
        public bool IsPersistTransportMessage => PersistentPeerIds != null && PersistentPeerIds.Count != 0;

        public TransportMessage(MessageTypeId messageTypeId, Stream content, Peer sender)
            : this(messageTypeId, content, sender.Id, sender.EndPoint)
        {
        }

        public TransportMessage(MessageTypeId messageTypeId, Stream content, PeerId senderId, string senderEndPoint)
            : this (messageTypeId, content, CreateOriginator(senderId, senderEndPoint))
        {
        }

        public TransportMessage(MessageTypeId messageTypeId, Stream content, OriginatorInfo originator)
        {
            Id = MessageId.NextId();
            MessageTypeId = messageTypeId;
            Content = content;
            Originator = originator;
        }

        [UsedImplicitly]
        internal TransportMessage()
        {
        }

        private static OriginatorInfo CreateOriginator(PeerId peerId, string peerEndPoint)
        {
            return new OriginatorInfo(peerId, peerEndPoint, MessageContext.CurrentMachineName, MessageContext.GetInitiatorUserName());
        }

        public byte[] GetContentBytes()
        {
            if (Content == null)
                return ArrayUtil.Empty<byte>();

            var position = Content.Position;
            var buffer = new byte[Content.Length];
            Content.Position = 0;
            Content.Read(buffer, 0, buffer.Length);
            Content.Position = position;

            return buffer;
        }

        internal TransportMessage ToPersistTransportMessage(List<PeerId> peerIds)
        {
            return new TransportMessage
            {
                Id = Id,
                MessageTypeId = MessageTypeId,
                Content = Content,
                Originator = Originator,
                Environment = Environment,
                WasPersisted = WasPersisted,
                PersistentPeerIds = peerIds,
            };
        }
    }
}