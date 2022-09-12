using System.Collections.Generic;
using System.IO;
using Abc.Zebus.Serialization.Protobuf;

namespace Abc.Zebus.Transport
{
    internal static class TransportMessageWriter
    {
        private static readonly MemoryStream _emptyStream = new MemoryStream(new byte[0]);

        internal static void WriteTransportMessage(this ProtoBufferWriter writer, TransportMessage transportMessage, string? environmentOverride = null)
        {
            writer.WriteRawTag(1 << 3 | 2);
            Write(writer, transportMessage.Id);

            writer.WriteRawTag(2 << 3 | 2);
            Write(writer, transportMessage.MessageTypeId);

            var transportMessageContent = transportMessage.Content;
            writer.WriteRawTag(3 << 3 | 2);
            writer.WriteLength((int)transportMessageContent.Length);
            writer.WriteRawBytes(transportMessageContent);

            writer.WriteRawTag(4 << 3 | 2);
            Write(writer, transportMessage.Originator);

            var environment = environmentOverride ?? transportMessage.Environment;
            if (environment != null)
            {
                writer.WriteRawTag(5 << 3 | 2);
                var environmentLength = GetUtf8ByteCount(environment);
                writer.WriteString(environment, environmentLength);
            }

            if (transportMessage.WasPersisted != null)
                WriteWasPersisted(writer, transportMessage.WasPersisted.Value);
        }

        internal static void SetWasPersisted(this ProtoBufferWriter writer, bool wasPersisted)
        {
            if (writer.TryWriteBoolAtSavedPosition(wasPersisted))
                return;

            WriteWasPersisted(writer, wasPersisted);
        }

        internal static void WritePersistentPeerIds(this ProtoBufferWriter writer, TransportMessage transportMessage, List<PeerId>? persistentPeerIdOverride)
        {
            var peerIds = persistentPeerIdOverride ?? transportMessage.PersistentPeerIds;
            if (peerIds == null)
                return;

            for (var index = 0; index < peerIds.Count; index++)
            {
                var peerIdString = peerIds[index].ToString();
                if (string.IsNullOrEmpty(peerIdString))
                    continue;

                writer.WriteRawTag(7 << 3 | 2);

                var peerIdStringLength = GetUtf8ByteCount(peerIdString);
                var peerIdLength = 1 + ProtoBufferWriter.ComputeStringSize(peerIdStringLength);

                writer.WriteLength(peerIdLength);
                writer.WriteRawTag(1 << 3 | 2);
                writer.WriteString(peerIdString, peerIdStringLength);
            }
        }

        private static void Write(ProtoBufferWriter writer, MessageId messageId)
        {
            var size = 1 + GetMessageSizeWithLength(ProtoBufferWriter.GuidSize);
            writer.WriteLength(size);
            writer.WriteRawTag(1 << 3 | 2);

            writer.WriteGuid(messageId.Value);
        }

        private static void Write(ProtoBufferWriter writer, MessageTypeId messageTypeId)
        {
            if (messageTypeId.FullName == null)
            {
                writer.WriteLength(0);
            }
            else
            {
                var fullNameLength = GetUtf8ByteCount(messageTypeId.FullName);
                var size = 1 + ProtoBufferWriter.ComputeStringSize(fullNameLength);
                writer.WriteLength(size);
                writer.WriteRawTag(1 << 3 | 2);
                writer.WriteString(messageTypeId.FullName, fullNameLength);
            }
        }

        private static void Write(ProtoBufferWriter writer, OriginatorInfo originatorInfo)
        {
            var size = 0;

            // SenderId
            var senderIdString = originatorInfo.SenderId.ToString();
            int senderIdLength;
            int senderIdStringLength;
            if (string.IsNullOrEmpty(senderIdString))
            {
                senderIdStringLength = 0;
                senderIdLength = 0;
                size += 1 + GetMessageSizeWithLength(senderIdLength);
            }
            else
            {
                senderIdStringLength = GetUtf8ByteCount(senderIdString);
                senderIdLength = 1 + ProtoBufferWriter.ComputeStringSize(senderIdStringLength);
                size += 1 + GetMessageSizeWithLength(senderIdLength);
            }

            // SenderEndPoint
            int senderEndPointLength;
            if (originatorInfo.SenderEndPoint == null)
            {
                senderEndPointLength = 0;
            }
            else
            {
                senderEndPointLength = GetUtf8ByteCount(originatorInfo.SenderEndPoint);
                size += 1 + ProtoBufferWriter.ComputeStringSize(senderEndPointLength);
            }

            // SenderMachineName
            int senderMachineNameLength;
            if (originatorInfo.SenderMachineName == null)
            {
                senderMachineNameLength = 0;
            }
            else
            {
                senderMachineNameLength = GetUtf8ByteCount(originatorInfo.SenderMachineName);
                size += 1 + ProtoBufferWriter.ComputeStringSize(senderMachineNameLength);
            }

            // InitiatorUserName
            int initiatorUserNameLength;
            if (originatorInfo.InitiatorUserName == null)
            {
                initiatorUserNameLength = 0;
            }
            else
            {
                initiatorUserNameLength = GetUtf8ByteCount(originatorInfo.InitiatorUserName);
                size += 1 + ProtoBufferWriter.ComputeStringSize(initiatorUserNameLength);
            }

            writer.WriteLength(size);

            writer.WriteRawTag(1 << 3 | 2);
            writer.WriteLength(senderIdLength);

            if (!string.IsNullOrEmpty(senderIdString))
            {
                writer.WriteRawTag(1 << 3 | 2);
                writer.WriteString(senderIdString, senderIdStringLength);
            }

            if (originatorInfo.SenderEndPoint != null)
            {
                writer.WriteRawTag(2 << 3 | 2);
                writer.WriteString(originatorInfo.SenderEndPoint, senderEndPointLength);
            }
            if (originatorInfo.SenderMachineName != null)
            {
                writer.WriteRawTag(3 << 3 | 2);
                writer.WriteString(originatorInfo.SenderMachineName, senderMachineNameLength);
            }
            if (originatorInfo.InitiatorUserName != null)
            {
                writer.WriteRawTag(5 << 3 | 2);
                writer.WriteString(originatorInfo.InitiatorUserName, initiatorUserNameLength);
            }
        }

        private static int GetMessageSizeWithLength(int size)
        {
            return size + ProtoBufferWriter.ComputeLengthSize(size);
        }

        private static unsafe int GetUtf8ByteCount(string s)
        {
            fixed (char* c = s)
            {
                for (var index = 0; index < s.Length; index++)
                {
                    if (c[index] >= 128)
                        return ProtoBufferWriter.Utf8Encoding.GetByteCount(c, s.Length);
                }
            }
            return s.Length;
        }

        private static void WriteWasPersisted(ProtoBufferWriter writer, bool value)
        {
            writer.WriteRawTag(6 << 3 | 0);
            writer.SavePosition();
            writer.WriteBool(value);
        }
    }
}
