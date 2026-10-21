#if NET
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace Abc.Zebus.Monitoring;

public class ZebusMetricPublisher : IZebusMetricPublisher, IDisposable
{
    public static string PeerIdTag => "zebus.peer.id";
    public static string MeterName => "Abc.Zebus";

    private readonly Meter _meter = new(MeterName, typeof(ZebusMetricPublisher).Assembly.GetName().Version?.ToString());
    private readonly Dictionary<string, HashSet<PeerId>> _connectedSocketsByPeerTagValue = new();
    private readonly string? _dynamicPeerIdTagPlaceholder;
    private readonly Counter<long> _messagesSent;
    private readonly Counter<long> _messagesReceived;
    private readonly Counter<long> _bytesSent;
    private readonly Counter<long> _bytesReceived;
    private readonly Counter<long> _messageSendFailures;
    private readonly Counter<long> _peerConnections;
    private readonly Counter<long> _peerDisconnections;
    private readonly Counter<long> _peerConnectionFailures;
    private readonly ObservableUpDownCounter<int> _connectedSockets;

    public ZebusMetricPublisher(IZebusMetricPublisherConfiguration configuration)
    {
        // The value is captured on construction because runtime changes could break metric computation.
        _dynamicPeerIdTagPlaceholder = configuration.DynamicPeerIdTagPlaceholder;

        _messagesSent = _meter.CreateCounter<long>("zebus.transport.messages.sent", unit: "{message}", description: "Number of transport messages sent to peers");
        _messagesReceived = _meter.CreateCounter<long>("zebus.transport.messages.received", unit: "{message}", description: "Number of transport messages received");
        _bytesSent = _meter.CreateCounter<long>("zebus.transport.bytes.sent", unit: "By", description: "Number of bytes sent to peers");
        _bytesReceived = _meter.CreateCounter<long>("zebus.transport.bytes.received", unit: "By", description: "Number of bytes received from peers");
        _messageSendFailures = _meter.CreateCounter<long>("zebus.transport.messages.send_failures", unit: "{message}", description: "Number of transport message send failures");
        _peerConnections = _meter.CreateCounter<long>("zebus.transport.peer_connections", unit: "{connection}", description: "Number of outbound peer socket connections established");
        _peerDisconnections = _meter.CreateCounter<long>("zebus.transport.peer_disconnections", unit: "{disconnection}", description: "Number of outbound peer socket disconnections");
        _peerConnectionFailures = _meter.CreateCounter<long>("zebus.transport.peer_connection_failures", unit: "{failure}", description: "Number of outbound peer socket connection failures");
        _connectedSockets = _meter.CreateObservableUpDownCounter("zebus.transport.peer_connected_sockets", observeValues: ObserveConnectedSockets, unit: "{socket}", description: "Number of connected outbound sockets per peer");

        IEnumerable<Measurement<int>> ObserveConnectedSockets()
        {
            // Explicitly avoid using yield return to control lock duration.
            var measurements = new List<Measurement<int>>();
            lock (_connectedSocketsByPeerTagValue)
            {
                foreach (var (peerTagValue, connectedSockets) in _connectedSocketsByPeerTagValue)
                {
                    measurements.Add(new Measurement<int>(connectedSockets.Count, PeerTag(peerTagValue)));
                }
            }

            return measurements;
        }
    }

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

    public void UpdateConnectionStatus(bool isConnected, PeerId peerId)
    {
        lock (_connectedSocketsByPeerTagValue)
        {
            var tagValue = PeerTagValue(peerId);
            if (!_connectedSocketsByPeerTagValue.TryGetValue(tagValue, out var connectedSockets))
            {
                connectedSockets = new();
                _connectedSocketsByPeerTagValue.Add(tagValue, connectedSockets);
            }

            if (isConnected)
            {
                connectedSockets.Add(peerId);
            }
            else
            {
                connectedSockets.Remove(peerId);
                if (connectedSockets.Count == 0)
                    _connectedSocketsByPeerTagValue.Remove(tagValue);
            }
        }
    }

    private KeyValuePair<string, object?> PeerTag(PeerId peerId)
    {
        var tagValue = PeerTagValue(peerId);

        return PeerTag(tagValue);
    }

    private static KeyValuePair<string, object?> PeerTag(string tagValue)
    {
        return new(PeerIdTag, tagValue);
    }

    private string PeerTagValue(PeerId peerId)
    {
        var tagValue = peerId.ToString();

        if (_dynamicPeerIdTagPlaceholder is { Length: > 0 } placeholder)
        {
            var separatorIndex = tagValue.LastIndexOf('.');
            if (separatorIndex >= 0 && Guid.TryParse(tagValue.Substring(separatorIndex + 1), out _))
                tagValue = tagValue.Substring(0, separatorIndex + 1) + placeholder;
        }

        return tagValue;
    }
}
#endif
