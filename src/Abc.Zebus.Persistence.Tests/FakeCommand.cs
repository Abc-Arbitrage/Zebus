using ProtoBuf;

namespace Abc.Zebus.Persistence.Tests
{
    [ProtoContract]
    public class FakeCommand : ICommand
    {
        [ProtoMember(1, IsRequired = true)]
        public readonly int Value;

        public FakeCommand(int value = 0)
        {
            Value = value;
        }
    }
}