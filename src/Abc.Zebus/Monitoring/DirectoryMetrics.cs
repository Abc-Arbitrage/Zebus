#if NET
using System.Diagnostics.Metrics;
#endif

namespace Abc.Zebus.Monitoring;

/// <summary>
/// Provides <see cref="System.Diagnostics.Metrics"/> instruments for monitoring Zebus peer directory.
/// </summary>
internal static class DirectoryMetrics
{
#if NET
    private static readonly UpDownCounter<int> _knownPeerCount = ZebusMetrics.Meter.CreateUpDownCounter<int>(
        "zebus.directory.known_peers",
        unit: "{peer}",
        description: "Current number of known peers in the directory");

    private static readonly Counter<long> _peerUpdates = ZebusMetrics.Meter.CreateCounter<long>(
        "zebus.directory.peer_updates",
        unit: "{update}",
        description: "Number of peer update events received");
#endif

    internal static void AddKnownPeerCount(int delta)
    {
#if NET
        _knownPeerCount.Add(delta);
#endif
    }

    internal static void AddPeerUpdates(long delta)
    {
#if NET
        _peerUpdates.Add(delta);
#endif
    }
}
