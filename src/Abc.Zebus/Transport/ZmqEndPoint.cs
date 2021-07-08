using System;

namespace Abc.Zebus.Transport
{
    internal readonly struct ZmqEndPoint
    {
        private readonly string? _value;

        public ZmqEndPoint(string? value)
        {
            _value = value?.Replace("0.0.0.0", Environment.MachineName).ToLower();
        }

        public override string ToString()
        {
            return _value ?? "tcp://*:*";
        }
    }
}
