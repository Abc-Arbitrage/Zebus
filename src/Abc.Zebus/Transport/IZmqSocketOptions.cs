using System;

namespace Abc.Zebus.Transport
{
    public interface IZmqSocketOptions
    {
        TimeSpan ReadTimeout { set; get; }
        int SendHighWaterMark { get; set; }
        TimeSpan SendTimeout { get; set; }
        int SendRetriesBeforeSwitchingToClosedState { get; set; }
        TimeSpan ClosedStateDuration { get; set; }
        int ReceiveHighWaterMark { get; set; }
    }
}