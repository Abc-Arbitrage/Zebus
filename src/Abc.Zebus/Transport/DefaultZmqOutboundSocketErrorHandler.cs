using System;

namespace Abc.Zebus.Transport;

public class DefaultZmqOutboundSocketErrorHandler : IZmqOutboundSocketErrorHandler
{
    public void OnConnectException(PeerId peerId, string endPoint, Exception exception)
    {
    }

    public void OnDisconnectException(PeerId peerId, string endPoint, Exception exception)
    {
    }

    public void OnSendFailed(PeerId peerId, string endPoint, MessageTypeId messageTypeId, MessageId id)
    {
    }
}
