using System;
using System.Collections.Generic;
using Abc.Zebus.Scan.Pipes;

namespace Abc.Zebus.Tests.Pipes
{
    public class FakePipeSource : IPipeSource
    {
        public readonly List<IPipe> Pipes = new List<IPipe>();

        public IEnumerable<IPipe> GetPipes(Type messageHandlerType)
        {
            return Pipes;
        }
    }
}