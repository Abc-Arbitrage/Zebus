using Abc.Zebus.Routing;
using ProtoBuf;

namespace Abc.Zebus.Tests.Dispatch.DispatchMessages
{
    [ProtoContract, Routable]
    public class RoutableEvent : IEvent
    {
        [ProtoMember(1), RoutingPosition(1)]
        public string Code;

        [ProtoMember(2)]
        public double Value { get; set; }
    }
}
