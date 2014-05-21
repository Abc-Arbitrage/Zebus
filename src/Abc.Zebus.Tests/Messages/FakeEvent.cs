using ProtoBuf;

namespace Abc.Zebus.Tests.Messages
{
    [ProtoContract]
    public class FakeEvent : IEvent
    {
        [ProtoMember(1, IsRequired = true)] public readonly int FakeId;
        
        FakeEvent()
        {
        }

        public FakeEvent (int fakeId)
        {
            FakeId = fakeId;
        }
    }
}