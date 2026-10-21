namespace Abc.Zebus.Monitoring;

public class NoopZebusMetricPublisher : IZebusMetricPublisher
{
    public void AddMessagesSent(long delta, PeerId peerId)
    {
    }

    public void AddMessagesReceived(long delta, PeerId peerId)
    {
    }

    public void AddBytesSent(long delta, PeerId peerId)
    {
    }

    public void AddBytesReceived(long delta, PeerId peerId)
    {
    }

    public void AddMessageSendFailures(long delta, PeerId peerId)
    {
    }

    public void AddPeerConnections(long delta, PeerId peerId)
    {
    }

    public void AddPeerDisconnections(long delta, PeerId peerId)
    {
    }

    public void AddPeerConnectionFailures(long delta, PeerId peerId)
    {
    }

    public void UpdateConnectionStatus(bool isConnected, PeerId peerId)
    {
    }
}
