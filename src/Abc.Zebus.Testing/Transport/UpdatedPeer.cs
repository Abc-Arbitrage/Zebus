using System;
using Abc.Zebus.Directory;

namespace Abc.Zebus.Testing.Transport;

public class UpdatedPeer : IEquatable<UpdatedPeer>
{
    public readonly PeerId PeerId;
    public readonly PeerUpdateAction UpdateAction;

    public UpdatedPeer(PeerId peerId, PeerUpdateAction updateAction)
    {
        PeerId = peerId;
        UpdateAction = updateAction;
    }

    public bool Equals(UpdatedPeer? other)
    {
        if (ReferenceEquals(null, other))
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return PeerId.Equals(other.PeerId) && UpdateAction == other.UpdateAction;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
            return false;
        if (ReferenceEquals(this, obj))
            return true;
        if (obj.GetType() != this.GetType())
            return false;
        return Equals((UpdatedPeer)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (PeerId.GetHashCode() * 397) ^ (int)UpdateAction;
        }
    }
}
