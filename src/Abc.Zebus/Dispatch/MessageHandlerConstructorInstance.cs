using System;
using StructureMap;
using StructureMap.Pipeline;

namespace Abc.Zebus.Dispatch
{
    internal class MessageHandlerConstructorInstance : ConstructorInstance, IConfiguredInstance
    {
        private readonly IBus _bus;
        private readonly MessageContext _messageContext;

        public MessageHandlerConstructorInstance(Type pluggedType, IBus bus, MessageContext messageContext) : base(pluggedType)
        {
            _bus = bus;
            _messageContext = messageContext;
        }

        object IConfiguredInstance.Get(string propertyName, Type pluginType, BuildSession session)
        {
            return GetSpecialParameter(pluginType, session);
        }

        T IConfiguredInstance.Get<T>(string propertyName, BuildSession session)
        {
            return (T)GetSpecialParameter(typeof(T), session);
        }

        object GetSpecialParameter(Type pluginType, BuildSession session)
        {
            if (pluginType == typeof(IBus))
                return _bus;
            if (pluginType == typeof(MessageContext))
                return _messageContext;

            return new DefaultInstance().Build(pluginType, session);
        }
    }
}