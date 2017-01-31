using System;

namespace Abc.Zebus.Tests.Dispatch.DispatchMessages
{
    public class AsyncFailingCommand : ICommand
    {
        public Exception Exception { get; }
        public bool ThrowSynchronously { get; set; }

        public AsyncFailingCommand(Exception exception)
        {
            Exception = exception;
        }
    }
}
