using ProtoBuf;

namespace Abc.Zebus.Tests.Messages
{
    [ProtoContract, Transient, Infrastructure]
    public class FakeInfrastructureTransientCommand : ICommand
    {
         
    }
}