#if NET10_0_OR_GREATER
using System.Diagnostics.Metrics;

namespace Abc.Zebus.Monitoring;

/// <summary>
/// Provides <see cref="System.Diagnostics.Metrics"/> instruments for monitoring Zebus peer directory.
/// </summary>
internal static class DirectoryMetrics
{
    internal static readonly UpDownCounter<int> KnownPeerCount = ZebusMetrics.Meter.CreateUpDownCounter<int>(
        "zebus.directory.known_peers",
        unit: "{peer}",
        description: "Current number of known peers in the directory");

    internal static readonly Counter<long> PeerUpdates = ZebusMetrics.Meter.CreateCounter<long>(
        "zebus.directory.peer_updates",
        unit: "{update}",
        description: "Number of peer update events received");
}
#endif
