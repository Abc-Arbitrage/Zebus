using System;
using System.Collections.Generic;
using System.IO;
using Abc.Zebus.Serialization.Protobuf;
using ProtoBuf;

namespace Abc.Zebus.Transport
{
    internal static class TransportMessageReader
    {
        internal static TransportMessage ReadTransportMessage(this ProtoBufferReader input)
        {
            return input.TryReadTransportMessage(out var transportMessage) ? transportMessage : new TransportMessage();
        }

        internal static bool TryReadTransportMessage(this ProtoBufferReader input, out TransportMessage transportMessage)
        {
            transportMessage = new TransportMessage
            {
                Content = Stream.Null,
                Originator = new OriginatorInfo(),
            };

            while (input.CanRead(1))
            {
                if (!input.TryReadMember(transportMessage))
                    return false;
            }

            return true;
        }

        private static bool TryReadMember(this ProtoBufferReader input, TransportMessage transportMessage)
        {
            if (!input.TryReadTag(out var number, out var wireType))
                return false;

            switch (number)
            {
                case 1:
                    if (!input.TryReadSingleGuid(out var id))
                        return false;
                    transportMessage.Id = new MessageId(id);
                    break;
                case 2:
                    if (!input.TryReadSingleString(out var messageTypeId))
                        return false;
                    transportMessage.MessageTypeId = new MessageTypeId(messageTypeId);
                    break;
                case 3:
                    if (!input.TryReadStream(out var content))
                        return false;
                    transportMessage.Content = content;
                    break;
                case 4:
                    if (!input.TryReadOriginatorInfo(out var originator) || originator == null)
                        return false;
                    transportMessage.Originator = originator;
                    break;
                case 5:
                    if (!input.TryReadString(out var environment))
                        return false;
                    transportMessage.Environment = environment;
                    break;
                case 6:
                    if (!input.TryReadBool(out var wasPersisted))
                        return false;
                    transportMessage.WasPersisted = wasPersisted;
                    break;
                case 7:
                    if (!input.TryReadSingleString(out var persistentPeerId))
                        return false;
                    transportMessage.PersistentPeerIds ??= new List<PeerId>();
                    transportMessage.PersistentPeerIds.Add(new PeerId(persistentPeerId));
                    break;
                default:
                    if (!input.TrySkipUnknown(wireType))
                        return false;
                    break;
            }

            return true;
        }

        private static bool TryReadOriginatorInfo(this ProtoBufferReader input, out OriginatorInfo? originatorInfo)
        {
            originatorInfo = default;

            if (!input.TryReadLength(out var length))
                return false;

            var endPosition = input.Position + length;

            var senderId = new PeerId();
            string? senderEndPoint = null;
            string? initiatorUserName = null;

            while (input.Position < endPosition && input.TryReadTag(out var number, out var wireType))
            {
                switch (number)
                {
                    case 1:
                        if (!input.TryReadSingleString(out var peerId))
                            return false;
                        senderId = new PeerId(peerId);
                        break;
                    case 2:
                        if (!input.TryReadString(out senderEndPoint))
                            return false;
                        break;
                    case 5:
                        if (!input.TryReadString(out initiatorUserName))
                            return false;
                        break;
                    default:
                        if (!input.TrySkipUnknown(wireType))
                            return false;
                        break;
                }
            }

            originatorInfo = new OriginatorInfo(senderId, senderEndPoint!, null, initiatorUserName);
            return true;
        }

        private static bool TryReadStream(this ProtoBufferReader input, out Stream? value)
        {
            if (!input.TryReadLength(out var length) || !input.TryReadRawBytes(length, out var bytes))
            {
                value = default;
                return false;
            }

            value = new MemoryStream(bytes);
            return true;
        }

        private static bool TryReadSingleString(this ProtoBufferReader input, out string? value)
        {
            value = default;

            if (!input.TryReadLength(out var length))
                return false;

            var endPosition = input.Position + length;
            while (input.Position < endPosition && input.TryReadTag(out var number, out var wireType))
            {
                switch (number)
                {
                    case 1:
                        if (!input.TryReadString(out value))
                            return false;
                        break;
                    default:
                        if (!input.TrySkipUnknown(wireType))
                            return false;
                        break;
                }
            }

            return true;
        }

        private static bool TryReadSingleGuid(this ProtoBufferReader input, out Guid value)
        {
            value = default;

            if (!input.TryReadLength(out var length))
                return false;

            var endPosition = input.Position + length;
            while (input.Position < endPosition && input.TryReadTag(out var number, out var wireType))
            {
                switch (number)
                {
                    case 1:
                        if (!input.TryReadGuid(out value))
                            return false;
                        break;
                    default:
                        if (!input.TrySkipUnknown(wireType))
                            return false;
                        break;
                }
            }

            return true;
        }

        private static bool TrySkipUnknown(this ProtoBufferReader input, WireType wireType)
        {
            switch (wireType)
            {
                case WireType.None:
                    return false;

                case WireType.Variant:
                    return input.TryReadRawVariant(out _);

                case WireType.Fixed64:
                    return input.TryReadFixed64(out _);

                case WireType.String:
                    return input.TrySkipString();

                case WireType.StartGroup:
                    return false;

                case WireType.EndGroup:
                    return false;

                case WireType.Fixed32:
                    return input.TryReadFixed32(out _);

                default:
                    return false;
            }
        }
    }
}
