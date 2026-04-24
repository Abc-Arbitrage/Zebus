#if NET10_0_OR_GREATER
using System;
using System.Diagnostics.Metrics;

namespace Abc.Zebus.Monitoring;

/// <summary>
/// Provides <see cref="System.Diagnostics.Metrics"/> instruments for monitoring Zebus connection state and messaging.
/// </summary>
internal static class ZebusMetrics
{
    internal static readonly Meter Meter = new("Abc.Zebus", typeof(ZebusMetrics).Assembly.GetName().Version?.ToString());

    // Transport: messages
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

    // Transport: connections
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

    // Transport: outbound socket state
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

    // Bus: status
    internal static readonly UpDownCounter<int> ActiveBusCount = Meter.CreateUpDownCounter<int>(
        "zebus.bus.active",
        unit: "{bus}",
        description: "Current number of active (started) bus instances");
}
#endif
