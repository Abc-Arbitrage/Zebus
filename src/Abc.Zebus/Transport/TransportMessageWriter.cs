using System;
using System.Collections.Generic;
using System.IO;
using Abc.Zebus.Serialization.Protobuf;

namespace Abc.Zebus.Transport
{
    internal static class TransportMessageWriter
    {
        private static readonly MemoryStream _emptyStream = new MemoryStream(new byte[0]);

        internal static void WriteTransportMessage(this CodedOutputStream output, TransportMessage transportMessage, string environmentOverride = null)
        {
            output.WriteRawTag(1 << 3 | 2);
            Write(output, transportMessage.Id);

            output.WriteRawTag(2 << 3 | 2);
            Write(output, transportMessage.MessageTypeId);

            var transportMessageContent = transportMessage.Content ?? _emptyStream;
            output.WriteRawTag(3 << 3 | 2);
            output.WriteLength((int)transportMessageContent.Length);
            output.WriteRawStream(transportMessageContent);

            output.WriteRawTag(4 << 3 | 2);
            Write(output, transportMessage.Originator);

            var environment = environmentOverride ?? transportMessage.Environment;
            if (environment != null)
            {
                output.WriteRawTag(5 << 3 | 2);
                var environmentLength = GetUtf8ByteCount(environment);
                output.WriteString(environment, environmentLength);
            }

            if (transportMessage.WasPersisted != null)
                WriteWasPersisted(output, transportMessage.WasPersisted.Value);
        }

        internal static void SetWasPersisted(this CodedOutputStream output, bool wasPersisted)
        {
            if (output.TryWriteBoolAtSavedPosition(wasPersisted))
                return;

            WriteWasPersisted(output, wasPersisted);
        }

        internal static void WritePersistentPeerIds(this CodedOutputStream output, TransportMessage transportMessage, List<PeerId> persistentPeerIdOverride)
        {
            var peerIds = persistentPeerIdOverride ?? transportMessage.PersistentPeerIds;
            if (peerIds == null)
                return;

            for (var index = 0; index < peerIds.Count; index++)
            {
                var peerIdString = peerIds[index].ToString();
                if (string.IsNullOrEmpty(peerIdString))
                    continue;

                output.WriteRawTag(7 << 3 | 2);

                var peerIdStringLength = GetUtf8ByteCount(peerIdString);
                var peerIdLength = 1 + CodedOutputStream.ComputeStringSize(peerIdStringLength);

                output.WriteLength(peerIdLength);
                output.WriteRawTag(1 << 3 | 2);
                output.WriteString(peerIdString, peerIdStringLength);
            }
        }

        private static void Write(CodedOutputStream output, MessageId messageId)
        {
            var size = 1 + GetMessageSizeWithLength(CodedOutputStream.GuidSize);
            output.WriteLength(size);
            output.WriteRawTag(1 << 3 | 2);

            output.WriteGuid(messageId.Value);
        }

        private static void Write(CodedOutputStream output, MessageTypeId messageTypeId)
        {
            if (messageTypeId.FullName == null)
            {
                output.WriteLength(0);
            }
            else
            {
                var fullNameLength = GetUtf8ByteCount(messageTypeId.FullName);
                var size = 1 + CodedOutputStream.ComputeStringSize(fullNameLength);
                output.WriteLength(size);
                output.WriteRawTag(1 << 3 | 2);
                output.WriteString(messageTypeId.FullName, fullNameLength);
            }
        }

        private static void Write(CodedOutputStream output, OriginatorInfo originatorInfo)
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
                senderIdLength = 1 + CodedOutputStream.ComputeStringSize(senderIdStringLength);
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
                size += 1 + CodedOutputStream.ComputeStringSize(senderEndPointLength);
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
                size += 1 + CodedOutputStream.ComputeStringSize(senderMachineNameLength);
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
                size += 1 + CodedOutputStream.ComputeStringSize(initiatorUserNameLength);
            }

            output.WriteLength(size);

            output.WriteRawTag(1 << 3 | 2);
            output.WriteLength(senderIdLength);

            if (!string.IsNullOrEmpty(senderIdString))
            {
                output.WriteRawTag(1 << 3 | 2);
                output.WriteString(senderIdString, senderIdStringLength);
            }

            if (originatorInfo.SenderEndPoint != null)
            {
                output.WriteRawTag(2 << 3 | 2);
                output.WriteString(originatorInfo.SenderEndPoint, senderEndPointLength);
            }
            if (originatorInfo.SenderMachineName != null)
            {
                output.WriteRawTag(3 << 3 | 2);
                output.WriteString(originatorInfo.SenderMachineName, senderMachineNameLength);
            }
            if (originatorInfo.InitiatorUserName != null)
            {
                output.WriteRawTag(5 << 3 | 2);
                output.WriteString(originatorInfo.InitiatorUserName, initiatorUserNameLength);
            }
        }

        private static int GetMessageSizeWithLength(int size)
        {
            return size + CodedOutputStream.ComputeLengthSize(size);
        }

        private static unsafe int GetUtf8ByteCount(string s)
        {
            fixed (char* c = s)
            {
                for (var index = 0; index < s.Length; index++)
                {
                    if (c[index] >= 128)
                        return CodedOutputStream.Utf8Encoding.GetByteCount(c, s.Length);
                }
            }
            return s.Length;
        }

        private static void WriteWasPersisted(CodedOutputStream output, bool value)
        {
            output.WriteRawTag(6 << 3 | 0);
            output.SavePosition();
            output.WriteBool(value);
        }
    }
}
