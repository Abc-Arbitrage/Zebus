using System;

namespace Abc.Zebus.Dispatch.Pipes
{
    public readonly struct AfterInvokeArgs
    {
        public readonly PipeInvocation Invocation;
        public readonly object? State;
        public readonly bool IsFaulted;
        public readonly Exception? Exception;

        public AfterInvokeArgs(PipeInvocation invocation, object? state, bool isFaulted, Exception? exception)
        {
            Invocation = invocation;
            State = state;
            IsFaulted = isFaulted;
            Exception = exception;
        }
    }
}
