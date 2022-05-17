using System.Collections.Generic;
using Abc.Zebus.Routing;
using ProtoBuf;

namespace Abc.Zebus.Tests.Messages
{
    [ProtoContract, Routable]
    public class FakeRoutableCommandWithCollection : ICommand
    {
        [ProtoMember(1), RoutingPosition(1)]
        public string Name;

        [ProtoMember(2), RoutingPosition(2)]
        public int[] IdArray;

        [ProtoMember(3), RoutingPosition(3)]
        public List<decimal> ValueList { get; set; }

    }
}
