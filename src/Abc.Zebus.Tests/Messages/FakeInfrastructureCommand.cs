using ProtoBuf;

namespace Abc.Zebus.Tests.Messages
{
    [ProtoContract, Infrastructure]
    public class FakeInfrastructureCommand : ICommand
    {
    }
}