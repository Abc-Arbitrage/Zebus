using System;
using ProtoBuf;

namespace Abc.Zebus.Lotus
{
    public class CustomProcessingFailed : IEvent
    {
        [ProtoMember(1, IsRequired = true)]
        public readonly string SourceTypeFullName;
        [ProtoMember(2, IsRequired = true)]
        public readonly string ExceptionMessage;
        [ProtoMember(3, IsRequired = true)]
        public readonly DateTime ExceptionUtcTime;

        public CustomProcessingFailed(string sourceTypeFullName, string exceptionMessage, DateTime exceptionUtcTime)
        {
            SourceTypeFullName = sourceTypeFullName;
            ExceptionMessage = exceptionMessage;
            ExceptionUtcTime = exceptionUtcTime;
        }
    }
}