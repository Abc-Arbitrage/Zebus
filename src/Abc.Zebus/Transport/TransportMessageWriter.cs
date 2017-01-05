using Abc.Zebus.Serialization.Protobuf;

namespace Abc.Zebus.Transport
{
    internal static class TransportMessageWriter
    {
        internal static void WriteTransportMessage(this CodedOutputStream output, TransportMessage transportMessage)
        {
            output.WriteRawTag(10);
            Write(output, transportMessage.Id);

            output.WriteRawTag(18);
            Write(output, transportMessage.MessageTypeId);

            if (transportMessage.Content != null && transportMessage.Content.Length > 0)
            {
                output.WriteRawTag(26);
                output.WriteLength((int)transportMessage.Content.Length);
                output.WriteRawStream(transportMessage.Content);
            }

            output.WriteRawTag(34);
            Write(output, transportMessage.Originator);

            if (transportMessage.Environment != null)
            {
                output.WriteRawTag(42);
                output.WriteString(transportMessage.Environment);
            }

            if (transportMessage.WasPersisted != null)
            {
                output.WriteRawTag(48);
                output.WriteBool(transportMessage.WasPersisted.Value);
            }
        }

        private static void Write(CodedOutputStream output, MessageId messageId)
        {
            var size = 1 + GetMessageSizeWithLength(CodedOutputStream.GuidSize);
            output.WriteLength(size);
            output.WriteRawTag(10);

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
                var fullNameLength = CodedOutputStream.Utf8Encoding.GetByteCount(messageTypeId.FullName);
                var size = 1 + CodedOutputStream.ComputeStringSize(fullNameLength);
                output.WriteLength(size);
                output.WriteRawTag(10);
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
                senderIdStringLength = CodedOutputStream.Utf8Encoding.GetByteCount(senderIdString);
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
                senderEndPointLength = CodedOutputStream.Utf8Encoding.GetByteCount(originatorInfo.SenderEndPoint);
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
                senderMachineNameLength = CodedOutputStream.Utf8Encoding.GetByteCount(originatorInfo.SenderMachineName);
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
                initiatorUserNameLength = CodedOutputStream.Utf8Encoding.GetByteCount(originatorInfo.InitiatorUserName);
                size += 1 + CodedOutputStream.ComputeStringSize(initiatorUserNameLength);
            }

            output.WriteLength(size);

            output.WriteRawTag(10);
            output.WriteLength(senderIdLength);

            if (senderIdString != null)
            {
                output.WriteRawTag(10);
                output.WriteString(senderIdString, senderIdStringLength);
            }

            if (originatorInfo.SenderEndPoint != null)
            {
                output.WriteRawTag(18);
                output.WriteString(originatorInfo.SenderEndPoint, senderEndPointLength);
            }
            if (originatorInfo.SenderMachineName != null)
            {
                output.WriteRawTag(26);
                output.WriteString(originatorInfo.SenderMachineName, senderMachineNameLength);
            }
            if (originatorInfo.InitiatorUserName != null)
            {
                output.WriteRawTag(42);
                output.WriteString(originatorInfo.InitiatorUserName, initiatorUserNameLength);
            }
        }

        private static int GetMessageSizeWithLength(int size)
        {
            return size + CodedOutputStream.ComputeLengthSize(size);
        }
    }
}