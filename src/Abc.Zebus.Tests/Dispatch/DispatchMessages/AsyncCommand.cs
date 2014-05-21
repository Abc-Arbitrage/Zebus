using System.Threading;

namespace Abc.Zebus.Tests.Dispatch.DispatchMessages
{
    public class AsyncCommand : ICommand
    {
        public readonly AutoResetEvent Signal = new AutoResetEvent(false);
    }
}