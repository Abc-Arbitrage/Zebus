using System.Threading;

namespace Abc.Zebus.Tests.Dispatch.DispatchMessages
{
    public class DispatchCommand : ICommand
    {
        public readonly AutoResetEvent Signal = new AutoResetEvent(false);
    }
}