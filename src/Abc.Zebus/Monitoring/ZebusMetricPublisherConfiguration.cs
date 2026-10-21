#if NET
namespace Abc.Zebus.Monitoring;

public class ZebusMetricPublisherConfiguration : IZebusMetricPublisherConfiguration
{
    public string DynamicPeerIdTagPlaceholder { get; set; } = "Client";
}
#endif
