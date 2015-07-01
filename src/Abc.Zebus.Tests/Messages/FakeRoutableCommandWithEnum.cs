using Abc.Zebus.Routing;
using ProtoBuf;

namespace Abc.Zebus.Tests.Messages
{
    public enum TestEnum1
    {
        Foo = 1,
        Bar = 2,
    }

    public enum TestEnum2
    {
        Baz = 1,
        Buz = 2,
    }

    [ProtoContract, Routable]
    public class FakeRoutableCommandWithEnum : ICommand
    {
        [ProtoMember(1, IsRequired = true), RoutingPosition(1)]
        public TestEnum1 Test1;

        [ProtoMember(2, IsRequired = true), RoutingPosition(2)]
        public TestEnum2 Test2;

        FakeRoutableCommandWithEnum()
        {
        }

        public FakeRoutableCommandWithEnum(TestEnum1 test1, TestEnum2 test2)
        {
            Test1 = test1;
            Test2 = test2;
        }
    }
}