using Abc.Zebus.Transport;
using Abc.Zebus.Util;
using ProtoBuf;

namespace Abc.Zebus.Lotus
{
    [ProtoContract]
    public class ReplayMessageCommand : ICommand
    {
        [ProtoMember(1, IsRequired = true)]
        public readonly TransportMessage MessageToReplay;

        [ProtoMember(2, IsRequired = false)]
        public readonly string[] HandlerTypes;

        public ReplayMessageCommand(TransportMessage messageToReplay, string[] handlerTypes)
        {
            MessageToReplay = messageToReplay;
            HandlerTypes = handlerTypes ?? ArrayUtil.Empty<string>();
        }
    }
}
