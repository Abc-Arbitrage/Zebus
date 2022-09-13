using System;
using System.Collections.Generic;
using Abc.Zebus.Persistence;
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
            get => Content.ToArray();
            set => Content = value.AsMemory();
        }

        [ProtoIgnore, JsonIgnore]
        public ReadOnlyMemory<byte> Content { get; set; }

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

        public TransportMessage(MessageTypeId messageTypeId, ReadOnlyMemory<byte> content, Peer sender)
            : this(messageTypeId, content, sender.Id, sender.EndPoint)
        {
        }

        public TransportMessage(MessageTypeId messageTypeId, ReadOnlyMemory<byte> content, PeerId senderId, string senderEndPoint)
            : this(messageTypeId, content, CreateOriginator(senderId, senderEndPoint))
        {
        }

        public TransportMessage(MessageTypeId messageTypeId, ReadOnlyMemory<byte> content, OriginatorInfo originator)
            : this(MessageId.NextId(), messageTypeId, content, originator)
        {
        }

        [UsedImplicitly]
        internal TransportMessage()
        {
            Originator = default!;
        }

        public TransportMessage(MessageId id, MessageTypeId messageTypeId, ReadOnlyMemory<byte> content, OriginatorInfo originator)
            : this(id, messageTypeId, content, originator, null, null, null)
        {
        }

        internal TransportMessage(MessageId id, MessageTypeId messageTypeId, ReadOnlyMemory<byte> content, OriginatorInfo originator, string? environment, bool? wasPersisted, List<PeerId>? persistentPeerIds)
        {
            Id = id;
            MessageTypeId = messageTypeId;
            Content = content;
            Originator = originator;
            Environment = environment;
            WasPersisted = wasPersisted;
            PersistentPeerIds = persistentPeerIds;
        }

        private static OriginatorInfo CreateOriginator(PeerId peerId, string peerEndPoint)
        {
            return new OriginatorInfo(peerId, peerEndPoint, MessageContext.CurrentMachineName, MessageContext.GetInitiatorUserName());
        }

        public byte[] GetContentBytes()
        {
            return Content.ToArray();
        }

        /// <summary>
        /// Gets a <see cref="TransportMessage"/> that represents a <see cref="PersistMessageCommand"/> that wraps the current transport message.
        /// </summary>
        internal TransportMessage ConvertToPersistTransportMessage(List<PeerId> peerIds) => new TransportMessage(Id, MessageTypeId, Content, Originator, Environment, WasPersisted, peerIds);

        /// <summary>
        /// Gets back the wrapped transport message that is inside a <see cref="PersistMessageCommand"/>.
        /// </summary>
        internal TransportMessage ConvertFromPersistTransportMessage() => new TransportMessage(Id, MessageTypeId, Content, Originator, Environment, WasPersisted, null);

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

        internal static TransportMessage Empty() => new TransportMessage { Originator = new OriginatorInfo() };
    }
}
