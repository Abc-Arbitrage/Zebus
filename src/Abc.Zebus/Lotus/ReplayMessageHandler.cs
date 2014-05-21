using System.Linq;
using Abc.Zebus.Dispatch;

namespace Abc.Zebus.Lotus
{
    public class ReplayMessageHandler : IMessageHandler<ReplayMessageCommand>
    {
        private readonly IMessageDispatcher _dispatcher;
        private readonly IMessageDispatchFactory _dispatchFactory;

        public ReplayMessageHandler(IMessageDispatcher dispatcher, IMessageDispatchFactory dispatchFactory)
        {
            _dispatcher = dispatcher;
            _dispatchFactory = dispatchFactory;
        }

        public void Handle(ReplayMessageCommand message)
        {
            var dispatch = _dispatchFactory.CreateMessageDispatch(message.MessageToReplay);

            if (message.HandlerTypes != null && message.HandlerTypes.Length > 0)
                dispatch.InvokerFilter = invoker => message.HandlerTypes.Contains(invoker.MessageHandlerType.FullName);

            _dispatcher.Dispatch(dispatch);
        }
    }
}