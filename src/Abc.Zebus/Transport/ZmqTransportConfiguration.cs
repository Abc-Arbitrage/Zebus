using System;
using Abc.Zebus.Util;

namespace Abc.Zebus.Transport;

public class ZmqTransportConfiguration : IZmqTransportConfiguration
{
    public ZmqTransportConfiguration(string inboundEndPoint = "tcp://*:*")
    {
        InboundEndPoint = inboundEndPoint;
        WaitForEndOfStreamAckTimeout = 5.Seconds();
    }

    public string InboundEndPoint { get; set; }
    public TimeSpan WaitForEndOfStreamAckTimeout { get; set; }
}
