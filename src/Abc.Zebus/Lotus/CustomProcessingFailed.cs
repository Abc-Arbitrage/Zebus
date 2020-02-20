using System;
using Abc.Zebus.Util;
using Newtonsoft.Json;
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

        [ProtoMember(4, IsRequired = false)]
        public PeerId? FailingPeerIdOverride { get; set; }

        [ProtoMember(5, IsRequired = false)]
        public string? DetailsJson { get; set; }

        public CustomProcessingFailed(string sourceTypeFullName, string exceptionMessage)
            : this(sourceTypeFullName, exceptionMessage, SystemDateTime.UtcNow)
        {
        }

        public CustomProcessingFailed(string sourceTypeFullName, string exceptionMessage, DateTime exceptionUtcTime)
        {
            SourceTypeFullName = sourceTypeFullName;
            ExceptionMessage = exceptionMessage;
            ExceptionUtcTime = exceptionUtcTime;
        }

        public CustomProcessingFailed WithDetails(object? details)
        {
            DetailsJson = details != null ? JsonConvert.SerializeObject(details) : null;
            return this;
        }
    }
}
