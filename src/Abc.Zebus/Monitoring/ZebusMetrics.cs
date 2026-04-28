#if NET10_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace Abc.Zebus.Monitoring;

/// <summary>
/// Provides <see cref="System.Diagnostics.Metrics"/> instruments for monitoring Zebus connection state and messaging.
/// All per-connection metrics include a <c>zebus.peer.id</c> tag for filtering by individual peer.
/// </summary>
internal static class ZebusMetrics
{
    internal static readonly Meter Meter = new("Abc.Zebus", typeof(ZebusMetrics).Assembly.GetName().Version?.ToString());

    private const string PeerIdTag = "zebus.peer.id";

    // Transport: per-connection message counters
    internal static readonly Counter<long> MessagesSent = Meter.CreateCounter<long>(
        "zebus.transport.messages.sent",
        unit: "{message}",
        description: "Number of transport messages sent to peers");

    internal static readonly Counter<long> MessagesReceived = Meter.CreateCounter<long>(
        "zebus.transport.messages.received",
        unit: "{message}",
        description: "Number of transport messages received");

    internal static readonly Counter<long> MessageSendFailures = Meter.CreateCounter<long>(
        "zebus.transport.messages.send_failures",
        unit: "{message}",
        description: "Number of transport message send failures");

    // Transport: per-connection state
    internal static readonly Counter<long> PeerConnections = Meter.CreateCounter<long>(
        "zebus.transport.peer_connections",
        unit: "{connection}",
        description: "Number of outbound peer socket connections established");

    internal static readonly Counter<long> PeerDisconnections = Meter.CreateCounter<long>(
        "zebus.transport.peer_disconnections",
        unit: "{disconnection}",
        description: "Number of outbound peer socket disconnections");

    internal static readonly Counter<long> PeerConnectionFailures = Meter.CreateCounter<long>(
        "zebus.transport.peer_connection_failures",
        unit: "{failure}",
        description: "Number of outbound peer socket connection failures");

    // Transport: aggregate outbound socket count
    internal static readonly UpDownCounter<int> OutboundSocketCount = Meter.CreateUpDownCounter<int>(
        "zebus.transport.outbound_sockets",
        unit: "{socket}",
        description: "Current number of active outbound sockets");

    // Directory: peers
    internal static readonly UpDownCounter<int> KnownPeerCount = Meter.CreateUpDownCounter<int>(
        "zebus.directory.known_peers",
        unit: "{peer}",
        description: "Current number of known peers in the directory");

    internal static readonly Counter<long> PeerUpdates = Meter.CreateCounter<long>(
        "zebus.directory.peer_updates",
        unit: "{update}",
        description: "Number of peer update events received");

    // Bus: per-instance status
    private const string BusIdTag = "zebus.bus.id";

    internal static readonly UpDownCounter<int> BusActive = Meter.CreateUpDownCounter<int>(
        "zebus.bus.active",
        unit: "{bus}",
        description: "Whether a bus instance is active (1) or stopped (0)");

    internal static KeyValuePair<string, object?> PeerTag(PeerId peerId)
        => new(PeerIdTag, peerId.ToString());

    internal static KeyValuePair<string, object?> BusTag(int busInstanceId)
        => new(BusIdTag, busInstanceId);
}
#endif
