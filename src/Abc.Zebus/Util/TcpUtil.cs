using System;
using System.Net;
using System.Net.Sockets;

namespace Abc.Zebus.Util
{
    internal static class TcpUtil
    {
        public static int GetRandomUnusedPort()
        {
            var listener = new TcpListener(IPAddress.Any, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        public static bool IsPortUnused(int port)
        {
            var listener = new TcpListener(IPAddress.Any, port);
            try
            {
                listener.Start();
            }
            catch (Exception)
            {
                return false;
            }
            listener.Stop();
            return true;
        }
    }
}