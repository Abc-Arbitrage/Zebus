using System;
using System.Collections.Generic;
using System.IO;
using Abc.Zebus.Serialization.Protobuf;
using JetBrains.Annotations;
using Newtonsoft.Json;
using ProtoBuf;

namespace Abc.Zebus.Transport
{
    [ProtoContract]
    public class TransportMessage
    {
        [ProtoMember(1, IsRequired = true)]
        public MessageId Id { get; set; }

        [ProtoMember(2, IsRequired = true)]
        public MessageTypeId MessageTypeId { get; set; }

        [ProtoMember(3, IsRequired = true)]
        private byte[] ContentBytes
        {
            get => GetContentBytes();
            set => Content = new MemoryStream(value, 0, value.Length, false, true);
        }

        [ProtoIgnore, JsonIgnore]
        public Stream? Content { get; set; }

        [ProtoMember(4, IsRequired = true)]
        public OriginatorInfo Originator { get; set; }

        [ProtoMember(5, IsRequired = false)]
        public string? Environment { get; set; }

        [ProtoMember(6, IsRequired = false)]
        public bool? WasPersisted { get; set; }

        [ProtoMember(7, IsRequired = false)]
        public List<PeerId>? PersistentPeerIds { get; set; }

        [JsonIgnore]
        public bool IsPersistTransportMessage => PersistentPeerIds != null && PersistentPeerIds.Count != 0;

        [JsonIgnore]
        public PeerId SenderId => Originator.SenderId;

        public TransportMessage(MessageTypeId messageTypeId, Stream content, Peer sender)
            : this(messageTypeId, content, sender.Id, sender.EndPoint)
        {
        }

        public TransportMessage(MessageTypeId messageTypeId, Stream content, PeerId senderId, string senderEndPoint)
            : this(messageTypeId, content, CreateOriginator(senderId, senderEndPoint))
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
            Originator = default!;
        }

        private static OriginatorInfo CreateOriginator(PeerId peerId, string peerEndPoint)
        {
            return new OriginatorInfo(peerId, peerEndPoint, MessageContext.CurrentMachineName, MessageContext.GetInitiatorUserName());
        }

        public byte[] GetContentBytes()
        {
            if (Content == null)
                return Array.Empty<byte>();

            var position = Content.Position;
            var buffer = new byte[Content.Length];
            Content.Position = 0;
            Content.Read(buffer, 0, buffer.Length);
            Content.Position = position;

            return buffer;
        }

        internal TransportMessage ToPersistTransportMessage(List<PeerId> peerIds) => CloneWithPeerIds(peerIds);
        internal TransportMessage UnpackPersistTransportMessage() => CloneWithPeerIds(null);

        private TransportMessage CloneWithPeerIds(List<PeerId>? peerIds) => new TransportMessage
        {
            Id = Id,
            MessageTypeId = MessageTypeId,
            Content = Content,
            Originator = Originator,
            Environment = Environment,
            WasPersisted = WasPersisted,
            PersistentPeerIds = peerIds,
        };

        public static byte[] Serialize(TransportMessage transportMessage)
        {
            var writer = new ProtoBufferWriter();
            writer.WriteTransportMessage(transportMessage);

            return writer.ToArray();
        }

        public static TransportMessage Deserialize(byte[] bytes)
        {
            var reader = new ProtoBufferReader(bytes, bytes.Length);
            return reader.ReadTransportMessage();
        }
    }
}
