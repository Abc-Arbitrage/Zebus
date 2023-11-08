using System;

namespace Abc.Zebus.Transport;

public interface IZmqTransportConfiguration
{
    /// <summary>
    /// The endpoint (tcp://hostname:port, tcp://*:port, etc.) of the inbound connection,
    /// used by other Peers to communicate with this Peer
    /// </summary>
    string InboundEndPoint { get; }

    /// <summary>
    /// When shutting down normallly, the Bus signals to the Peers it has been communicating
    /// with that it is shutting down and waits for an acknowledgement (this is done to prevent
    /// message loss in the various buffers).
    /// To prevent from hanging on shutdown in case of a dead Peer not responding to the
    /// shutdown signal, the Bus will shutdown after this timeout.
    /// </summary>
    TimeSpan WaitForEndOfStreamAckTimeout { get; }
}
