using System;
using ProtoBuf;

namespace Abc.Zebus.Lotus
{
    public class CustomDelegatedProcessingFailed : IEvent
    {
        [ProtoMember(1, IsRequired = true)]
        public readonly string SourceTypeFullName;
        [ProtoMember(2, IsRequired = true)]
        public readonly string ExceptionMessage;
        [ProtoMember(3, IsRequired = true)]
        public readonly DateTime ExceptionUtcTime;
        [ProtoMember(4, IsRequired = true)]
        public readonly string SenderName;
        [ProtoMember(5, IsRequired = true)]
        public readonly string SenderMachineName;

        public CustomDelegatedProcessingFailed(string sourceTypeFullName, string exceptionMessage, DateTime exceptionUtcTime, string senderName, string senderMachineName)
        {
            SourceTypeFullName = sourceTypeFullName;
            ExceptionMessage = exceptionMessage;
            ExceptionUtcTime = exceptionUtcTime;
            SenderName = senderName;
            SenderMachineName = senderMachineName;
        }
    }
}