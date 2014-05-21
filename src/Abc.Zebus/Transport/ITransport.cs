using System;
using System.Collections.Generic;
using Abc.Zebus.Directory;

namespace Abc.Zebus.Transport
{
    public interface ITransport
    {
        event Action<TransportMessage> MessageReceived;
        event Action<PeerId, string> SocketConnected;
        event Action<PeerId, string> SocketDisconnected;

        void Configure(PeerId peerId, string environment);
        void OnRegistered();
        void OnPeerUpdated(PeerId peerId, PeerUpdateAction peerUpdateAction);

        void Start();
        void Stop();

        PeerId PeerId { get; }
        string InboundEndPoint { get; }
        int PendingSendCount { get; }

        void Send(TransportMessage message, IEnumerable<Peer> peerIds);
        void AckMessage(TransportMessage transportMessage);

        TransportMessage CreateInfrastructureTransportMessage(MessageTypeId messageTypeId);
    }
}