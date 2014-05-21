using System;

namespace Abc.Zebus.Tests.Dispatch.DispatchMessages
{
    public class AsyncFailingCommand : ICommand
    {
        public readonly Exception Exception;

        public AsyncFailingCommand(Exception exception)
        {
            Exception = exception;
        }
    }
}