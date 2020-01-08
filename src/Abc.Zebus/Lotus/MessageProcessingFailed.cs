using System;
using Abc.Zebus.Transport;
using ProtoBuf;

namespace Abc.Zebus.Lotus
{
    [ProtoContract]
    public class MessageProcessingFailed : IEvent
    {
        [ProtoMember(1, IsRequired = true)]
        public readonly TransportMessage FailingMessage;

        [ProtoMember(2, IsRequired = true)]
        public readonly string FailingMessageJson;

        [ProtoMember(3, IsRequired = true)]
        public readonly string ExceptionMessage;

        [ProtoMember(4, IsRequired = true)]
        public readonly DateTime ExceptionUtcTime;

        [ProtoMember(5, IsRequired = false)]
        public readonly string[] FailingHandlerNames;

        public MessageProcessingFailed(TransportMessage failingMessage, string failingMessageJson, string exceptionMessage, DateTime exceptionUtcTime, string[]? failingHandlerNames)
        {
            FailingMessage = failingMessage;
            FailingMessageJson = failingMessageJson;
            ExceptionMessage = exceptionMessage;
            ExceptionUtcTime = exceptionUtcTime;
            FailingHandlerNames = failingHandlerNames ?? Array.Empty<string>();
        }
    }
}
