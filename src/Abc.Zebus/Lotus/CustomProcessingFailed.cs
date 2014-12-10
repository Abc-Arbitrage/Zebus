using System;
using ProtoBuf;

namespace Abc.Zebus.Lotus
{
    [ProtoContract]
    public class CustomProcessingFailed : IEvent
    {
        [ProtoMember(1, IsRequired = true)]
        public string SourceTypeFullName { get; set; }
        [ProtoMember(2, IsRequired = true)]
        public string ExceptionMessage { get; set; }
        [ProtoMember(3, IsRequired = true)]
        public DateTime ExceptionUtcTime { get; set; }

        public CustomProcessingFailed(string sourceTypeFullName, string exceptionMessage, DateTime exceptionUtcTime)
        {
            SourceTypeFullName = sourceTypeFullName;
            ExceptionMessage = exceptionMessage;
            ExceptionUtcTime = exceptionUtcTime;
        }
    }
}