using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Util;

namespace Abc.Zebus.Tests.Dispatch.DispatchMessages
{
    public class AsyncCommandHandler : IAsyncMessageHandler<AsyncCommand>, IAsyncMessageHandler<DispatchCommand>
    {
        public readonly ManualResetEventSlim CalledSignal = new ManualResetEventSlim();
        public bool WaitForSignal;

        public Task Handle(AsyncCommand message)
        {
            return Task.Factory.StartNew(() =>
                {
                    if (WaitForSignal)
                        message.Signal.WaitOne(500.Milliseconds());

                    CalledSignal.Set();
                });
        }

        public Task Handle(DispatchCommand message)
        {
            return Task.Factory.StartNew(() =>
            {
                if (WaitForSignal)
                    message.Signal.WaitOne(500.Milliseconds());

                CalledSignal.Set();
            });
        }
    }
}