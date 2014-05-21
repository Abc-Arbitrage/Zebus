using System;
using Abc.Zebus.Routing;
using ProtoBuf;

namespace Abc.Zebus.Tests.Messages
{
    [ProtoContract, Routable]
    public class FakeRoutableCommand : ICommand
    {
        [ProtoMember(1, IsRequired = true), RoutingPosition(2)]
        public readonly string Name;

        [ProtoMember(2, IsRequired = true), RoutingPosition(1)]
        public readonly decimal Id;

        [ProtoMember(3, IsRequired = true), RoutingPosition(3)]
        public readonly Guid OtherId;

        FakeRoutableCommand()
        {
        }

        public FakeRoutableCommand(decimal id, string name)
        {
            Id = id;
            Name = name;
        }

        public FakeRoutableCommand(decimal id, string name, Guid otherId)
        {
            Id = id;
            Name = name;
            OtherId = otherId;
        }
    }
}