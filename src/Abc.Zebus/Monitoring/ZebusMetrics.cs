#if NET
using System.Collections.Generic;
using System.Diagnostics.Metrics;
#endif

namespace Abc.Zebus.Monitoring;

/// <summary>
/// Shared <see cref="System.Diagnostics.Metrics.Meter"/> and tag helpers for Zebus metrics.
/// Service-specific instruments are defined in <see cref="TransportMetrics"/> and <see cref="DirectoryMetrics"/>.
/// </summary>
internal static class ZebusMetrics
{
#if NET
    internal static readonly Meter Meter = new("Abc.Zebus", typeof(ZebusMetrics).Assembly.GetName().Version?.ToString());

    private const string PeerIdTag = "zebus.peer.id";

    internal static KeyValuePair<string, object?> PeerTag(PeerId peerId)
        => new(PeerIdTag, peerId.ToString());
#endif
}
