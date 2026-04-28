#if NET10_0_OR_GREATER
using System.Diagnostics.Metrics;

namespace Abc.Zebus.Monitoring;

/// <summary>
/// Provides <see cref="System.Diagnostics.Metrics"/> instruments for monitoring Zebus transport layer.
/// All per-connection metrics include a <c>zebus.peer.id</c> tag for filtering by individual peer.
/// </summary>
internal static class TransportMetrics
{
    // Per-connection message counters
    internal static readonly Counter<long> MessagesSent = ZebusMetrics.Meter.CreateCounter<long>(
        "zebus.transport.messages.sent",
        unit: "{message}",
        description: "Number of transport messages sent to peers");

    internal static readonly Counter<long> MessagesReceived = ZebusMetrics.Meter.CreateCounter<long>(
        "zebus.transport.messages.received",
        unit: "{message}",
        description: "Number of transport messages received");

    internal static readonly Counter<long> MessageSendFailures = ZebusMetrics.Meter.CreateCounter<long>(
        "zebus.transport.messages.send_failures",
        unit: "{message}",
        description: "Number of transport message send failures");

    // Per-connection state
    internal static readonly Counter<long> PeerConnections = ZebusMetrics.Meter.CreateCounter<long>(
        "zebus.transport.peer_connections",
        unit: "{connection}",
        description: "Number of outbound peer socket connections established");

    internal static readonly Counter<long> PeerDisconnections = ZebusMetrics.Meter.CreateCounter<long>(
        "zebus.transport.peer_disconnections",
        unit: "{disconnection}",
        description: "Number of outbound peer socket disconnections");

    internal static readonly Counter<long> PeerConnectionFailures = ZebusMetrics.Meter.CreateCounter<long>(
        "zebus.transport.peer_connection_failures",
        unit: "{failure}",
        description: "Number of outbound peer socket connection failures");

    // Aggregate outbound socket count
    internal static readonly UpDownCounter<int> OutboundSocketCount = ZebusMetrics.Meter.CreateUpDownCounter<int>(
        "zebus.transport.outbound_sockets",
        unit: "{socket}",
        description: "Current number of active outbound sockets");
}
#endif
