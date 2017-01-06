using System;
using System.Collections.Generic;
using System.IO;
using Abc.Zebus.Serialization.Protobuf;
using ProtoBuf;

namespace Abc.Zebus.Transport
{
    internal static class TransportMessageReader
    {
        internal static TransportMessage ReadTransportMessage(this CodedInputStream input)
        {
            var transportMessage = new TransportMessage();

            uint number;
            WireType wireType;
            while (!input.IsAtEnd && input.TryReadTag(out number, out wireType))
            {
                switch (number)
                {
                    case 1:
                        transportMessage.Id = ReadMessageId(input);
                        break;
                    case 2:
                        transportMessage.MessageTypeId = ReadMessageTypeId(input);
                        break;
                    case 3:
                        transportMessage.Content = ReadStream(input);
                        break;
                    case 4:
                        transportMessage.Originator = ReadOriginatorInfo(input);
                        break;
                    case 5:
                        transportMessage.Environment = input.ReadString();
                        break;
                    case 6:
                        transportMessage.WasPersisted = input.ReadBool();
                        break;
                    case 7:
                        transportMessage.PersistentPeerIds = ReadPeerIds(input, transportMessage.PersistentPeerIds);
                        break;
                    default:
                        SkipUnknown(input, wireType);
                        break;
                }
            }

            return transportMessage;
        }

        private static OriginatorInfo ReadOriginatorInfo(CodedInputStream input)
        {
            var length = input.ReadLength();
            var endPosition = input.Position + length;

            uint number;
            WireType wireType;
            var senderId = new PeerId();
            string senderEndPoint = null;
            string initiatorUserName = null;

            while (input.Position < endPosition && input.TryReadTag(out number, out wireType))
            {
                switch (number)
                {
                    case 1:
                        senderId = ReadPeerId(input);
                        break;
                    case 2:
                        senderEndPoint = input.ReadString();
                        break;
                    case 5:
                        initiatorUserName = input.ReadString();
                        break;
                    default:
                        SkipUnknown(input, wireType);
                        break;
                }
            }

            return new OriginatorInfo(senderId, senderEndPoint, null, initiatorUserName);
        }

        private static PeerId ReadPeerId(CodedInputStream input)
        {
            var value = ReadSingleField(input, x => x.ReadString());
            return new PeerId(value);
        }

        private static MessageId ReadMessageId(CodedInputStream input)
        {
            var guid = ReadSingleField(input, x => ReadGuid(input));
            return new MessageId(guid);
        }

        private static MessageTypeId ReadMessageTypeId(CodedInputStream input)
        {
            var fullName = ReadSingleField(input, x => x.ReadString());
            return new MessageTypeId(fullName);
        }

        private static Guid ReadGuid(CodedInputStream input)
        {
            return input.ReadGuid();
        }

        private static Stream ReadStream(CodedInputStream input)
        {
            var length = input.ReadLength();
            return new MemoryStream(input.ReadRawBytes(length));
        }

        private static T ReadSingleField<T>(CodedInputStream input, Func<CodedInputStream, T> read)
        {
            var length = input.ReadLength();
            var endPosition = input.Position + length;

            uint number;
            WireType wireType;
            var value = default(T);

            while (input.Position < endPosition && input.TryReadTag(out number, out wireType))
            {
                switch (number)
                {
                    case 1:
                        value = read.Invoke(input);
                        break;
                    default:
                        SkipUnknown(input, wireType);
                        break;
                }
            }

            return value;
        }

        private static List<PeerId> ReadPeerIds(CodedInputStream input, List<PeerId> peerIds)
        {
            if (peerIds == null)
                peerIds = new List<PeerId>();

            var value = ReadSingleField(input, x => x.ReadString());
            peerIds.Add(new PeerId(value));

            return peerIds;
        }

        private static void SkipUnknown(CodedInputStream input, WireType wireType)
        {
            switch (wireType)
            {
                case WireType.None:
                    break;
                case WireType.Variant:
                    input.ReadRawVarint32();
                    break;
                case WireType.Fixed64:
                    input.ReadFixed64();
                    break;
                case WireType.String:
                    input.SkipString();
                    break;
                case WireType.StartGroup:
                    break;
                case WireType.EndGroup:
                    break;
                case WireType.Fixed32:
                    input.ReadFixed32();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}