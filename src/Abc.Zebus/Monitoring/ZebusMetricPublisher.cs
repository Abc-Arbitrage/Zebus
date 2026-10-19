#if NET
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace Abc.Zebus.Monitoring;

public class ZebusMetricPublisher : IZebusMetricPublisher, IDisposable
{
    private readonly Meter _meter = new("Abc.Zebus", typeof(ZebusMetricPublisher).Assembly.GetName().Version?.ToString());
    private readonly HashSet<PeerId> _connectedPeers = new();
    private readonly IZebusMetricPublisherConfiguration _configuration;
    private readonly Counter<long> _messagesSent;
    private readonly Counter<long> _messagesReceived;
    private readonly Counter<long> _bytesSent;
    private readonly Counter<long> _bytesReceived;
    private readonly Counter<long> _messageSendFailures;
    private readonly Counter<long> _peerConnections;
    private readonly Counter<long> _peerDisconnections;
    private readonly Counter<long> _peerConnectionFailures;
    private readonly Gauge<int> _peerConnectionStatuses;
    private readonly Gauge<int> _totalConnectedSockets;

    public ZebusMetricPublisher(IZebusMetricPublisherConfiguration configuration)
    {
        _configuration = configuration;
        _messagesSent = _meter.CreateCounter<long>("zebus.transport.messages.sent", unit: "{message}", description: "Number of transport messages sent to peers");
        _messagesReceived = _meter.CreateCounter<long>("zebus.transport.messages.received", unit: "{message}", description: "Number of transport messages received");
        _bytesSent = _meter.CreateCounter<long>("zebus.transport.bytes.sent", unit: "By", description: "Number of bytes sent to peers");
        _bytesReceived = _meter.CreateCounter<long>("zebus.transport.bytes.received", unit: "By", description: "Number of bytes received from peers");
        _messageSendFailures = _meter.CreateCounter<long>("zebus.transport.messages.send_failures", unit: "{message}", description: "Number of transport message send failures");
        _peerConnections = _meter.CreateCounter<long>("zebus.transport.peer_connections", unit: "{connection}", description: "Number of outbound peer socket connections established");
        _peerDisconnections = _meter.CreateCounter<long>("zebus.transport.peer_disconnections", unit: "{disconnection}", description: "Number of outbound peer socket disconnections");
        _peerConnectionFailures = _meter.CreateCounter<long>("zebus.transport.peer_connection_failures", unit: "{failure}", description: "Number of outbound peer socket connection failures");
        _peerConnectionStatuses = _meter.CreateGauge<int>("zebus.transport.peer_connection_status", unit: "{connection}", description: "Whether a peer connection is alive (1) or dead (0)");
        _totalConnectedSockets = _meter.CreateGauge<int>("zebus.transport.total_connected_sockets", unit: "{socket}", description: "Total number of connected outbound sockets");
    }

    public static string PeerIdTag => "zebus.peer.id";

    public void Dispose()
    {
        _meter.Dispose();
    }

    public void AddMessagesSent(long delta, PeerId peerId)
    {
        _messagesSent.Add(delta, PeerTag(peerId));
    }

    public void AddMessagesReceived(long delta, PeerId peerId)
    {
        _messagesReceived.Add(delta, PeerTag(peerId));
    }

    public void AddBytesSent(long delta, PeerId peerId)
    {
        _bytesSent.Add(delta, PeerTag(peerId));
    }

    public void AddBytesReceived(long delta, PeerId peerId)
    {
        _bytesReceived.Add(delta, PeerTag(peerId));
    }

    public void AddMessageSendFailures(long delta, PeerId peerId)
    {
        _messageSendFailures.Add(delta, PeerTag(peerId));
    }

    public void AddPeerConnections(long delta, PeerId peerId)
    {
        _peerConnections.Add(delta, PeerTag(peerId));
    }

    public void AddPeerDisconnections(long delta, PeerId peerId)
    {
        _peerDisconnections.Add(delta, PeerTag(peerId));
    }

    public void AddPeerConnectionFailures(long delta, PeerId peerId)
    {
        _peerConnectionFailures.Add(delta, PeerTag(peerId));
    }

    public void UpdatedConnectionStatus(bool isConnected, PeerId peerId)
    {
        var connectedPeerCount = UpdatedConnectedPeers(isConnected, peerId);

        _peerConnectionStatuses.Record(isConnected ? 1 : 0, PeerTag(peerId));
        _totalConnectedSockets.Record(connectedPeerCount);

    }

    private int UpdatedConnectedPeers(bool isConnected, PeerId peerId)
    {
        // In practice, the caller is single-threaded, but a lock is used here to ensure thread safety in case of future changes.
        lock (_connectedPeers)
        {
            if (isConnected)
                _connectedPeers.Add(peerId);
            else
                _connectedPeers.Remove(peerId);

            return _connectedPeers.Count;
        }
    }

    private KeyValuePair<string, object?> PeerTag(PeerId peerId)
    {
        var tagValue = peerId.ToString();

        if (_configuration.DynamicPeerIdTagPlaceholder is { Length: > 0 } placeholder)
        {
            var separatorIndex = tagValue.LastIndexOf('.');
            if (separatorIndex >= 0 && Guid.TryParse(tagValue.Substring(separatorIndex + 1), out _))
                tagValue = tagValue.Substring(0, separatorIndex + 1) + placeholder;
        }

        return new(PeerIdTag, tagValue);
    }
}
#endif
