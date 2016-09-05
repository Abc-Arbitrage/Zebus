using System;
using System.Runtime.Serialization;

namespace Abc.Zebus.EventSourcing
{
    [DataContract]
    public class DomainEventSourcing
    {
        public DomainEventSourcing()
        {
        }

        public DomainEventSourcing(Guid eventId, Guid aggregateId, DateTime dateTime, int version, string userId)
        {
            EventId = eventId;
            AggregateId = aggregateId;
            DateTime = dateTime;
            Version = version;
            UserId = userId;
        }

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

        public override string ToString() => $"Version: {Version}, DateTime: {DateTime}";
    }
}