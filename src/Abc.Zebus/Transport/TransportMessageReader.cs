using System;
using System.Collections.Generic;
using System.IO;
using Abc.Zebus.Serialization.Protobuf;
using ProtoBuf;

namespace Abc.Zebus.Transport
{
    internal static class TransportMessageReader
    {
        internal static TransportMessage ReadTransportMessage(this ProtoBufferReader reader)
        {
            return reader.TryReadTransportMessage(out var transportMessage) ? transportMessage : new TransportMessage();
        }

        internal static bool TryReadTransportMessage(this ProtoBufferReader reader, out TransportMessage transportMessage)
        {
            transportMessage = new TransportMessage
            {
                Content = Stream.Null,
                Originator = new OriginatorInfo(),
            };

            while (reader.CanRead(1))
            {
                if (!reader.TryReadMember(transportMessage))
                    return false;
            }

            return true;
        }

        private static bool TryReadMember(this ProtoBufferReader reader, TransportMessage transportMessage)
        {
            if (!reader.TryReadTag(out var number, out var wireType))
                return false;

            switch (number)
            {
                case 1:
                    if (!reader.TryReadSingleGuid(out var id))
                        return false;
                    transportMessage.Id = new MessageId(id);
                    break;
                case 2:
                    if (!reader.TryReadSingleString(out var messageTypeId))
                        return false;
                    transportMessage.MessageTypeId = new MessageTypeId(messageTypeId);
                    break;
                case 3:
                    if (!reader.TryReadStream(out var content))
                        return false;
                    transportMessage.Content = content;
                    break;
                case 4:
                    if (!reader.TryReadOriginatorInfo(out var originator) || originator == null)
                        return false;
                    transportMessage.Originator = originator;
                    break;
                case 5:
                    if (!reader.TryReadString(out var environment))
                        return false;
                    transportMessage.Environment = environment;
                    break;
                case 6:
                    if (!reader.TryReadBool(out var wasPersisted))
                        return false;
                    transportMessage.WasPersisted = wasPersisted;
                    break;
                case 7:
                    if (!reader.TryReadSingleString(out var persistentPeerId))
                        return false;
                    transportMessage.PersistentPeerIds ??= new List<PeerId>();
                    transportMessage.PersistentPeerIds.Add(new PeerId(persistentPeerId));
                    break;
                default:
                    if (!reader.TrySkipUnknown(wireType))
                        return false;
                    break;
            }

            return true;
        }

        private static bool TryReadOriginatorInfo(this ProtoBufferReader reader, out OriginatorInfo? originatorInfo)
        {
            originatorInfo = default;

            if (!reader.TryReadLength(out var length))
                return false;

            var endPosition = reader.Position + length;

            var senderId = new PeerId();
            string? senderEndPoint = null;
            string? initiatorUserName = null;

            while (reader.Position < endPosition && reader.TryReadTag(out var number, out var wireType))
            {
                switch (number)
                {
                    case 1:
                        if (!reader.TryReadSingleString(out var peerId))
                            return false;
                        senderId = new PeerId(peerId);
                        break;
                    case 2:
                        if (!reader.TryReadString(out senderEndPoint))
                            return false;
                        break;
                    case 5:
                        if (!reader.TryReadString(out initiatorUserName))
                            return false;
                        break;
                    default:
                        if (!reader.TrySkipUnknown(wireType))
                            return false;
                        break;
                }
            }

            originatorInfo = new OriginatorInfo(senderId, senderEndPoint!, null, initiatorUserName);
            return true;
        }

        private static bool TryReadStream(this ProtoBufferReader reader, out Stream? value)
        {
            if (!reader.TryReadLength(out var length) || !reader.TryReadRawBytes(length, out var bytes))
            {
                value = default;
                return false;
            }

            value = new MemoryStream(bytes);
            return true;
        }

        private static bool TryReadSingleString(this ProtoBufferReader reader, out string? value)
        {
            value = default;

            if (!reader.TryReadLength(out var length))
                return false;

            var endPosition = reader.Position + length;
            while (reader.Position < endPosition && reader.TryReadTag(out var number, out var wireType))
            {
                switch (number)
                {
                    case 1:
                        if (!reader.TryReadString(out value))
                            return false;
                        break;
                    default:
                        if (!reader.TrySkipUnknown(wireType))
                            return false;
                        break;
                }
            }

            return true;
        }

        private static bool TryReadSingleGuid(this ProtoBufferReader reader, out Guid value)
        {
            value = default;

            if (!reader.TryReadLength(out var length))
                return false;

            var endPosition = reader.Position + length;
            while (reader.Position < endPosition && reader.TryReadTag(out var number, out var wireType))
            {
                switch (number)
                {
                    case 1:
                        if (!reader.TryReadGuid(out value))
                            return false;
                        break;
                    default:
                        if (!reader.TrySkipUnknown(wireType))
                            return false;
                        break;
                }
            }

            return true;
        }

        private static bool TrySkipUnknown(this ProtoBufferReader reader, WireType wireType)
        {
            switch (wireType)
            {
                case WireType.None:
                    return false;

                case WireType.Fixed64:
                    return reader.TryReadFixed64(out _);

                case WireType.String:
                    return reader.TrySkipString();

                case WireType.StartGroup:
                    return false;

                case WireType.EndGroup:
                    return false;

                case WireType.Fixed32:
                    return reader.TryReadFixed32(out _);

                default:
                    return false;
            }
        }
    }
}
