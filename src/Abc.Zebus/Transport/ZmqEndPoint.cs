using System;
using System.Net;

namespace Abc.Zebus.Transport
{
    internal readonly struct ZmqEndPoint
    {
        private readonly string? _value;

        public ZmqEndPoint(string? value)
        {
            if (value?.StartsWith("tcp://0.0.0.0:", StringComparison.OrdinalIgnoreCase) == true)
            {
                var fqdn = Dns.GetHostEntry(string.Empty).HostName;
                _value = value.Replace("0.0.0.0", fqdn);
            }
            else
            {
                _value = value;
            }
        }

        public override string ToString()
        {
            return _value ?? "tcp://*:*";
        }
    }
}
