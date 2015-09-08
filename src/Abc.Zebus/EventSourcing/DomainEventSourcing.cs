using System;
using System.Runtime.Serialization;

namespace Abc.Zebus.EventSourcing
{
    [DataContract]
    public class DomainEventSourcing
    {
        [DataMember(Order = 1)]
        public Guid EventId { get; internal set; }
        [DataMember(Order = 2)]
        public Guid AggregateId { get; internal set; }
        [DataMember(Order = 3)]
        public DateTime DateTime { get; internal set; }
        [DataMember(Order = 4)]
        public int Version { get; internal set; }
        [DataMember(Order = 5)]
        public string UserId { get; internal set; }

        public override string ToString()
        {
            return $"Version: {Version}, DateTime: {DateTime}";
        }
    }
}