using ProtoBuf;

namespace Abc.Zebus.Tests.Messages
{
    [ProtoContract]
    public class EmptyCommand : ICommand
    {
        public EmptyCommand()
        {
        }
    }
}