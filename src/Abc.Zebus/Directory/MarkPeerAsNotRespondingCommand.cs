using System;
using ProtoBuf;

namespace Abc.Zebus.Directory
{
    [ProtoContract]
    public class MarkPeerAsNotRespondingCommand : ICommand
    {
        [ProtoMember(1, IsRequired = true)]
        public readonly PeerId PeerId;

        [ProtoMember(2, IsRequired = true)]
        public readonly DateTime TimestampUtc;

        public MarkPeerAsNotRespondingCommand(PeerId peerId, DateTime timestampUtc)
        {
            PeerId = peerId;
            TimestampUtc = timestampUtc;
        }

        public override string ToString()
        {
            return PeerId.ToString();
        }
    }
}