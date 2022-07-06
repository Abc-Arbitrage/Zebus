using System;
using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.DependencyInjection;

namespace Abc.Zebus.Dispatch.Pipes
{
    public class AttributePipeSource : IPipeSource
    {
        private readonly IDependencyInjectionContainer _container;

        public AttributePipeSource(IDependencyInjectionContainer container)
        {
            _container = container;
        }

        public IEnumerable<IPipe> GetPipes(Type messageHandlerType)
        {
            var attributes = (PipeAttribute[])messageHandlerType.GetCustomAttributes(typeof(PipeAttribute), true);
            return attributes.Select(x => (IPipe)_container.GetInstance(x.PipeType));
        }
    }
}
