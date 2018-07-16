using System;

namespace Abc.Zebus.Tests.Dispatch.DispatchMessages
{
    public class FailingCommand : ICommand
    {
        public readonly Exception Exception;

        public FailingCommand(Exception exception)
        {
            Exception = exception;
        }
    }
}