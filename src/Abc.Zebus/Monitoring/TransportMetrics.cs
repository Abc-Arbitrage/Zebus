#if NET
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
#endif

namespace Abc.Zebus.Monitoring;

/// <summary>
/// Provides <see cref="System.Diagnostics.Metrics"/> instruments for monitoring Zebus transport layer.
/// All per-connection metrics include a <c>zebus.peer.id</c> tag for filtering by individual peer.
/// </summary>
internal static class TransportMetrics
{
#if NET
    // Per-connection message counters
    private static readonly Counter<long> _messagesSent = ZebusMetrics.Meter.CreateCounter<long>(
        "zebus.transport.messages.sent",
        unit: "{message}",
        description: "Number of transport messages sent to peers");

    private static readonly Counter<long> _messagesReceived = ZebusMetrics.Meter.CreateCounter<long>(
        "zebus.transport.messages.received",
        unit: "{message}",
        description: "Number of transport messages received");

    private static readonly Counter<long> _bytesSent = ZebusMetrics.Meter.CreateCounter<long>(
        "zebus.transport.bytes.sent",
        unit: "By",
        description: "Number of bytes sent to peers");

    private static readonly Counter<long> _bytesReceived = ZebusMetrics.Meter.CreateCounter<long>(
        "zebus.transport.bytes.received",
        unit: "By",
        description: "Number of bytes received from peers");

    private static readonly Counter<long> _messageSendFailures = ZebusMetrics.Meter.CreateCounter<long>(
        "zebus.transport.messages.send_failures",
        unit: "{message}",
        description: "Number of transport message send failures");

    // Per-connection state
    private static readonly Counter<long> _peerConnections = ZebusMetrics.Meter.CreateCounter<long>(
        "zebus.transport.peer_connections",
        unit: "{connection}",
        description: "Number of outbound peer socket connections established");

    private static readonly Counter<long> _peerDisconnections = ZebusMetrics.Meter.CreateCounter<long>(
        "zebus.transport.peer_disconnections",
        unit: "{disconnection}",
        description: "Number of outbound peer socket disconnections");

    private static readonly Counter<long> _peerConnectionFailures = ZebusMetrics.Meter.CreateCounter<long>(
        "zebus.transport.peer_connection_failures",
        unit: "{failure}",
        description: "Number of outbound peer socket connection failures");

    // Aggregate outbound socket count
    private static readonly UpDownCounter<int> _outboundSocketCount = ZebusMetrics.Meter.CreateUpDownCounter<int>(
        "zebus.transport.outbound_sockets",
        unit: "{socket}",
        description: "Current number of active outbound sockets");
#endif

    internal static void AddMessagesSent(long delta, PeerId peerId)
    {
#if NET
        _messagesSent.Add(delta, ZebusMetrics.PeerTag(peerId));
#endif
    }

    internal static void AddMessagesReceived(long delta, PeerId peerId)
    {
#if NET
        _messagesReceived.Add(delta, ZebusMetrics.PeerTag(peerId));
#endif
    }

    internal static void AddBytesSent(long delta, PeerId peerId)
    {
#if NET
        _bytesSent.Add(delta, ZebusMetrics.PeerTag(peerId));
#endif
    }

    internal static void AddBytesReceived(long delta, PeerId peerId)
    {
#if NET
        _bytesReceived.Add(delta, ZebusMetrics.PeerTag(peerId));
#endif
    }

    internal static void AddMessageSendFailures(long delta, PeerId peerId)
    {
#if NET
        _messageSendFailures.Add(delta, ZebusMetrics.PeerTag(peerId));
#endif
    }

    internal static void AddPeerConnections(long delta, PeerId peerId)
    {
#if NET
        _peerConnections.Add(delta, ZebusMetrics.PeerTag(peerId));
#endif
    }

    internal static void AddPeerDisconnections(long delta, PeerId peerId)
    {
#if NET
        _peerDisconnections.Add(delta, ZebusMetrics.PeerTag(peerId));
#endif
    }

    internal static void AddPeerConnectionFailures(long delta, PeerId peerId)
    {
#if NET
        _peerConnectionFailures.Add(delta, ZebusMetrics.PeerTag(peerId));
#endif
    }

    internal static void AddOutboundSocketCount(int delta)
    {
#if NET
        _outboundSocketCount.Add(delta);
#endif
    }

#if NET
    internal static ObservableGauge<int> CreateConnectionStateGauge(Func<IEnumerable<Measurement<int>>> observeValues)
        => ZebusMetrics.Meter.CreateObservableGauge(
            "zebus.transport.connection.alive",
            observeValues: observeValues,
            unit: "{connection}",
            description: "Whether a peer connection is alive (1) or dead (0)");
#endif
}
