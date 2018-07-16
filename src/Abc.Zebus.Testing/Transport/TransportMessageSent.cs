using System;
using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Transport;

namespace Abc.Zebus.Testing.Transport
{
    public class TransportMessageSent
    {
        public readonly TransportMessage TransportMessage;
        public readonly List<Peer> Targets = new List<Peer>();
        public readonly SendContext Context;

        public TransportMessageSent(TransportMessage transportMessage)
            : this(transportMessage, Enumerable.Empty<Peer>(), new SendContext())
        {
        }

        public TransportMessageSent(TransportMessage transportMessage, params Peer[] peers)
            : this(transportMessage, peers, new SendContext())
        {
        }

        public TransportMessageSent(TransportMessage transportMessage, Peer peer, bool wasPersistent = false)
            : this(transportMessage)
        {
            To(peer, wasPersistent);
        }

        public TransportMessageSent(TransportMessage transportMessage, IEnumerable<Peer> targets, SendContext context)
        {
            TransportMessage = transportMessage;
            Targets.AddRange(targets);
            Context = context;
        }

        public TransportMessageSent To(Peer peer, bool wasPersistent)
        {
            Targets.Add(peer);
            if (wasPersistent)
                Context.PersistentPeerIds.Add(peer.Id);

            return this;
        }

        public TransportMessageSent ToPersistence(Peer persistencePeer)
        {
            Context.PersistencePeer = persistencePeer;
            return this;
        }

        public TransportMessageSent AddPersistentPeer(Peer peer)
        {
            Context.PersistentPeerIds.Add(peer.Id);
            return this;
        }
    }
}