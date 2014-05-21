using System;
using System.Collections.Generic;
using System.Linq;
using StructureMap;

namespace Abc.Zebus.Pipes
{
    public class AttributePipeSource : IPipeSource
    {
        private readonly IContainer _container;

        public AttributePipeSource(IContainer container)
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