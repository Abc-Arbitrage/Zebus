using System;
using System.Collections.Generic;
using System.Reflection;

namespace Abc.Zebus.Dispatch;

public interface IMessageDispatcher
{
    void ConfigureAssemblyFilter(Func<Assembly, bool> assemblyFilter);
    void ConfigureHandlerFilter(Func<Type, bool> handlerFilter);
    void ConfigureMessageFilter(Func<Type, bool> messageFilter);
    event Action MessageHandlerInvokersUpdated;

    void LoadMessageHandlerInvokers();

    IEnumerable<MessageTypeId> GetHandledMessageTypes();
    IEnumerable<IMessageHandlerInvoker> GetMessageHandlerInvokers();

    void Dispatch(MessageDispatch dispatch);
    void Dispatch(MessageDispatch dispatch, Func<Type, bool> handlerFilter);

    void AddInvoker(IMessageHandlerInvoker newEventHandlerInvoker);
    void RemoveInvoker(IMessageHandlerInvoker eventHandlerInvoker);

    int Purge();

    void Stop();
    void Start();

    event Action Starting;
    event Action Stopping;
}
