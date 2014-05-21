using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using ZeroMQ;

namespace Abc.Zebus.Transport
{
    public static class ZmqUtil
    {
        public static void SetPeerId(this ZmqSocket socket, PeerId peerId)
        {
            socket.Identity = Encoding.ASCII.GetBytes(peerId.ToString());
        }

        public static int SendWithTimeout(this ZmqSocket socket, byte[] buffer, int length, TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            var spinWait = new SpinWait();
            int result;
            do
            {
                result = socket.Send(buffer, length, SocketFlags.DontWait);
                if (socket.SendStatus != SendStatus.TryAgain)
                    break;

                spinWait.SpinOnce();
            }
            while (stopwatch.Elapsed <= timeout);

            return result;
        }
    }
}