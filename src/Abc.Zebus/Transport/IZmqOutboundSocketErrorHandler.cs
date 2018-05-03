using System;

namespace Abc.Zebus.Transport
{
    public interface IZmqOutboundSocketErrorHandler
    {
        void OnConnectException(PeerId peerId, string endPoint, Exception exception);
        void OnDisconnectException(PeerId peerId, string endPoint, Exception exception);
        void OnSendFailed(PeerId peerId, string endPoint, MessageTypeId messageTypeId, MessageId id);
    }
}