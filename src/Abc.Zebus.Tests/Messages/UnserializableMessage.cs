using ProtoBuf;

namespace Abc.Zebus.Tests.Messages
{
    [ProtoContract]
    public class UnserializableMessage : ICommand
    {
        [ProtoMember(1)]
        public int Id { get; set; }

        [ProtoMember(1)]
        public int TypeId { get; set; }
    }
}
