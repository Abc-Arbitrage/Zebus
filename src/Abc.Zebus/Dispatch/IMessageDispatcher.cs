using System;
using System.Collections.Generic;
using System.Reflection;

namespace Abc.Zebus.Dispatch
{
    public interface IMessageDispatcher
    {
        void ConfigureAssemblyFilter(Func<Assembly, bool> assemblyFilter);
        void ConfigureHandlerFilter(Func<Type, bool> handlerFilter);
        void ConfigureMessageFilter(Func<Type, bool> messageFilter);

        void LoadMessageHandlerInvokers();

        IEnumerable<MessageTypeId> GetHandledMessageTypes();
        IEnumerable<IMessageHandlerInvoker> GetMessageHanlerInvokers();

        void Dispatch(MessageDispatch dispatch);
        void Dispatch(MessageDispatch dispatch, Func<Type, bool> handlerFilter);

        void AddInvoker(IMessageHandlerInvoker eventHandlerInvoker);
        void RemoveInvoker(IMessageHandlerInvoker eventHandlerInvoker);

        void Stop();
        void Start();
        int Purge();
    }
}