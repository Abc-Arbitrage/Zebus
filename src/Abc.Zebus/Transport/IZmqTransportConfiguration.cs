using System;

namespace Abc.Zebus.Transport
{
    public interface IZmqTransportConfiguration 
    {
        string InboundEndPoint { get; }

        TimeSpan WaitForEndOfStreamAckTimeout { get; }
    }
}