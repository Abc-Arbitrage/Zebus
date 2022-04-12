using System;
using System.Diagnostics;

namespace Abc.Zebus.Directory.Cassandra.Tests
{
    public class ConsoleTraceListener : TraceListener
    {
        public override void Write(string message)
        {
            Console.WriteLine(message);
        }

        public override void WriteLine(string message)
        {
            Console.WriteLine(message);
        }
    }
}
