using System;
using System.Collections.Generic;
using StructureMap;

namespace Abc.Zebus.Pipes
{
    public class PipeSource<TPipe> : IPipeSource where TPipe : class, IPipe 
    {
        private readonly IContainer _container;

        public PipeSource(IContainer container)
        {
            _container = container;
        }

        public IEnumerable<IPipe> GetPipes(Type messageHandlerType)
        {
            yield return _container.GetInstance<TPipe>();
        }
    }
}