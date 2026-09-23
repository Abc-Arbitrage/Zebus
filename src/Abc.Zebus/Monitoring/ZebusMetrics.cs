#if NET
using System;
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

    internal static KeyValuePair<string, object?> PeerTag(PeerId peerId, bool replacePeerIdGuidSuffixWithClient = false)
    {
        var value = peerId.ToString();

        if (replacePeerIdGuidSuffixWithClient)
        {
            var separatorIndex = value.LastIndexOf('.');
            if (separatorIndex >= 0 && Guid.TryParse(value.Substring(separatorIndex + 1), out _))
                value = value.Substring(0, separatorIndex) + ".Client";
        }

        return new(PeerIdTag, value);
    }
#endif
}
