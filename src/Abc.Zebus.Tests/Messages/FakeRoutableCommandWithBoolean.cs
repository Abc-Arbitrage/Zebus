using Abc.Zebus.Routing;
using ProtoBuf;

namespace Abc.Zebus.Tests.Messages
{
    [ProtoContract, Routable]
    public class FakeRoutableCommandWithBoolean : ICommand
    {
        [ProtoMember(1, IsRequired = true), RoutingPosition(1)]
        public bool IsAMatch;

        FakeRoutableCommandWithBoolean()
        {
        }

        public FakeRoutableCommandWithBoolean(bool isAMatch)
        {
            IsAMatch = isAMatch;
        }
    }
}