using Abc.Zebus.Serialization.Protobuf;

namespace Abc.Zebus.Transport
{
    internal static class TransportMessageWriter
    {
        internal static void WriteTransportMessage(this CodedOutputStream output, TransportMessage transportMessage)
        {
            output.WriteRawTag(1 << 3 | 2);
            Write(output, transportMessage.Id);

            output.WriteRawTag(2 << 3 | 2);
            Write(output, transportMessage.MessageTypeId);

            if (transportMessage.Content != null && transportMessage.Content.Length > 0)
            {
                output.WriteRawTag(3 << 3 | 2);
                output.WriteLength((int)transportMessage.Content.Length);
                output.WriteRawStream(transportMessage.Content);
            }

            output.WriteRawTag(4 << 3 | 2);
            Write(output, transportMessage.Originator);

            if (transportMessage.Environment != null)
            {
                output.WriteRawTag(5 << 3 | 2);
                var environmentLength = GetUtf8ByteCount(transportMessage.Environment);
                output.WriteString(transportMessage.Environment, environmentLength);
            }

            if (transportMessage.WasPersisted != null)
            {
                output.WriteRawTag(6 << 3 | 0);
                output.WriteBool(transportMessage.WasPersisted.Value);
            }
        }

        internal static void WritePersistentPeerIds(this CodedOutputStream output, TransportMessage transportMessage)
        {
            var peerIds = transportMessage.PersistentPeerIds;
            if (peerIds == null)
                return;

            for (var index = 0; index < peerIds.Count; index++)
            {
                var peerIdString = peerIds[index].ToString();
                if (peerIdString == null)
                    continue;

                output.WriteTag(7 << 3 | 2);

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
            if (senderIdString == null)
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

            if (senderIdString != null)
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
    }
}