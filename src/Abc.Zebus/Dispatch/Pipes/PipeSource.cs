using System;
using System.Collections.Generic;
using Abc.Zebus.DependencyInjection; 

namespace Abc.Zebus.Dispatch.Pipes
{
    public class PipeSource<TPipe> : IPipeSource where TPipe : class, IPipe
    {
        private readonly IDependencyInjectionContainer _container;

        public PipeSource(IDependencyInjectionContainer container)
        {
            _container = container;
        }

        public IEnumerable<IPipe> GetPipes(Type messageHandlerType)
        {
            yield return _container.GetInstance<TPipe>();
        }
    }
}
