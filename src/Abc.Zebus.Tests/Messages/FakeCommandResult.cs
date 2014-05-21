using ProtoBuf;

namespace Abc.Zebus.Tests.Messages
{
    [ProtoContract]
    public class FakeCommandResult : IMessage
    {
        [ProtoMember(1, IsRequired = true)]
        public readonly string StringValue;
        [ProtoMember(2, IsRequired = true)]
        public readonly int IntegerValue;

        public FakeCommandResult(string stringValue, int integerValue)
        {
            StringValue = stringValue;
            IntegerValue = integerValue;
        }
    }
}