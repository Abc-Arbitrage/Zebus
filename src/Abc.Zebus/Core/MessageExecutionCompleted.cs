using System;
using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Serialization;
using ProtoBuf;

namespace Abc.Zebus.Core
{
    [ProtoContract, Transient, Infrastructure]
    public class MessageExecutionCompleted : IMessage
    {
        public static readonly MessageTypeId TypeId = new MessageTypeId(typeof(MessageExecutionCompleted));

        [ProtoMember(1, IsRequired = true)]
        public MessageId SourceCommandId { get; private set; }

        [ProtoMember(2, IsRequired = true)]
        public int ErrorCode { get; private set; }

        [ProtoMember(3, IsRequired = false)]
        public MessageTypeId PayloadTypeId { get; private set; }

        [ProtoMember(4, IsRequired = false)]
        public byte[] Payload { get; private set; }

        public MessageExecutionCompleted(MessageId sourceCommandId, int errorCode)
        {
            SourceCommandId = sourceCommandId;
            ErrorCode = errorCode;
        }

        public MessageExecutionCompleted(MessageId sourceCommandId, MessageTypeId payloadTypeId, byte[] payload)
        {
            SourceCommandId = sourceCommandId;
            ErrorCode = 0;
            PayloadTypeId = payloadTypeId;
            Payload = payload;
        }

        public override string ToString()
        {
            return ErrorCode == 0
                ? string.Format("CommandId: {0}", SourceCommandId)
                : string.Format("CommandId: {0}, ErrorCode: {1}", SourceCommandId, ErrorCode);
        }

        public static MessageExecutionCompleted Create(MessageContext messageContext, DispatchResult dispatchResult, IMessageSerializer serializer)
        {
            if (dispatchResult.Errors.Any())
                return Failure(messageContext.MessageId, dispatchResult.Errors);

            if (messageContext.ReplyResponse != null)
                return Success(messageContext.MessageId, messageContext.ReplyResponse, serializer);

            return new MessageExecutionCompleted(messageContext.MessageId, messageContext.ReplyCode);
        }

        public static MessageExecutionCompleted Success(MessageId sourceCommandId, IMessage payload, IMessageSerializer serializer)
        {
            var payloadBytes = serializer.Serialize(payload);

            return new MessageExecutionCompleted(sourceCommandId, payload.TypeId(), payloadBytes);
        }

        public static MessageExecutionCompleted Failure(MessageId sourceCommandId, IEnumerable<Exception> exceptions)
        {
            var errorCode = CommandResult.GetErrorCode(exceptions);

            return new MessageExecutionCompleted(sourceCommandId, errorCode);
        }
    }
}