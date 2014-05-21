using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Util.Extensions;
using StructureMap;

namespace Abc.Zebus.Scan
{
    public class MultiEventHandlerInvokerLoader : IMessageHandlerInvokerLoader
    {
        private readonly IContainer _container;

        public MultiEventHandlerInvokerLoader(IContainer container)
        {
            _container = container;
        }

        public IEnumerable<IMessageHandlerInvoker> LoadMessageHandlerInvokers(TypeSource typeSource)
        {
            return from type in typeSource.GetTypes()
                   where type.IsClass && !type.IsAbstract && type.IsVisible && type.Is<IMultiEventHandler>()
                   let handler = (IMultiEventHandler)_container.GetInstance(type)
                   let messageTypesHandled = handler.GetHandledEventTypes()
                   from messageType in messageTypesHandled
                   select new MultiEventHandlerInvoker(messageType, handler);
        }
    }
}