#if NET
namespace Abc.Zebus.Monitoring;

public interface IZebusMetricPublisherConfiguration
{
    /// <summary>
    /// Allows to automatically replace the GUID part of the peer IDs with a placeholder in metric tags
    /// (e.g.: if the placeholder is <c>Client</c>, <c>Abc.Katapult.Dashboard.a2e09e4e-1174-48da-8c15-b4e3009750b8</c> will be replaced by <c>Abc.Katapult.Dashboard.Client</c>).
    /// If the placeholder is null or empty, the peer IDs will be used as-is in metric tags.
    /// </summary>
    string DynamicPeerIdTagPlaceholder { get; }
}

#endif
