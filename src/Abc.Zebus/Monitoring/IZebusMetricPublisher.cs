namespace Abc.Zebus.Monitoring;

public interface IZebusMetricPublisher
{
    void AddMessagesSent(long delta, PeerId peerId);
    void AddMessagesReceived(long delta, PeerId peerId);
    void AddBytesSent(long delta, PeerId peerId);
    void AddBytesReceived(long delta, PeerId peerId);
    void AddMessageSendFailures(long delta, PeerId peerId);
    void AddPeerConnections(long delta, PeerId peerId);
    void AddPeerDisconnections(long delta, PeerId peerId);
    void AddPeerConnectionFailures(long delta, PeerId peerId);
    void UpdateConnectionStatus(bool isConnected, PeerId peerId);
}
