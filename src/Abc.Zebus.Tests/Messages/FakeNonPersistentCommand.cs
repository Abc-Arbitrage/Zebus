using ProtoBuf;

namespace Abc.Zebus.Tests.Messages
{
    [ProtoContract, Transient]
    public class FakeNonPersistentCommand : ICommand
    {
        [ProtoMember(1, IsRequired = true)] public readonly int FakeId;
        
        FakeNonPersistentCommand()
        {
        }

        public FakeNonPersistentCommand(int fakeId)
        {
            FakeId = fakeId;
        }
    }
}