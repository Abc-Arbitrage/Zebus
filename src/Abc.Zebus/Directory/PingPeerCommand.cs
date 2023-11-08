using ProtoBuf;

namespace Abc.Zebus.Directory;

[ProtoContract, Transient, Infrastructure]
public sealed class PingPeerCommand : ICommand
{
}
