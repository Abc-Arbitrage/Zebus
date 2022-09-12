using ProtoBuf;

namespace Abc.Zebus.Tests.Messages
{
    [ProtoContract]
    public class FakeCommand : ICommand
    {
        [ProtoMember(1, IsRequired = true)] public readonly int FakeId;

        FakeCommand()
        {
        }

        public FakeCommand (int fakeId)
        {
            FakeId = fakeId;
        }
    }
}
