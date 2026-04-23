using System.Diagnostics.Metrics;

namespace Abc.Zebus.Monitoring;

/// <summary>
/// Provides <see cref="System.Diagnostics.Metrics"/> instruments for monitoring Zebus connections and messaging.
/// </summary>
public static class ZebusMetrics
{
    /// <summary>
    /// The name of the <see cref="Meter"/> used by Zebus.
    /// </summary>
    public const string MeterName = "Abc.Zebus";

    internal static readonly Meter Meter = new(MeterName);

    // -- Bus lifecycle --

    internal static readonly Counter<long> BusStartedCount = Meter.CreateCounter<long>(
        "zebus.bus.started",
        description: "Number of times the bus has been started");

    internal static readonly Counter<long> BusStoppedCount = Meter.CreateCounter<long>(
        "zebus.bus.stopped",
        description: "Number of times the bus has been stopped");

    // -- Messaging --

    internal static readonly Counter<long> MessageSentCount = Meter.CreateCounter<long>(
        "zebus.messages.sent",
        description: "Number of logical messages sent (one per Send/Publish call)");

    internal static readonly Counter<long> MessageReceivedCount = Meter.CreateCounter<long>(
        "zebus.messages.received",
        description: "Number of messages received");

    // -- Transport --

    internal static readonly Counter<long> TransportSendFailureCount = Meter.CreateCounter<long>(
        "zebus.transport.send_failures",
        description: "Number of transport send failures");

    internal static readonly Counter<long> SocketClosedStateCount = Meter.CreateCounter<long>(
        "zebus.transport.socket_closed_state",
        description: "Number of times a socket switched to closed state");

    internal static readonly Counter<long> TransportMessageDeserializationFailureCount = Meter.CreateCounter<long>(
        "zebus.transport.deserialization_failures",
        description: "Number of transport message deserialization failures");

    // -- Directory --

    internal static readonly Counter<long> DirectoryRegistrationCount = Meter.CreateCounter<long>(
        "zebus.directory.registrations",
        description: "Number of directory registrations");

    internal static readonly Histogram<double> DirectoryRegistrationDuration = Meter.CreateHistogram<double>(
        "zebus.directory.registration_duration",
        unit: "ms",
        description: "Duration of directory registrations in milliseconds");

    internal static readonly Counter<long> PeerUpdatedCount = Meter.CreateCounter<long>(
        "zebus.directory.peer_updates",
        description: "Number of peer update events");
}
