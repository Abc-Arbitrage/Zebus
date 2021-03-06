﻿using System.Threading;
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
            return Task.Run(() =>
            {
                if (WaitForSignal)
                    message.Signal.WaitOne(2.Seconds());

                CalledSignal.Set();
            });
        }

        public Task Handle(DispatchCommand message)
        {
            return Task.Run(() =>
            {
                if (WaitForSignal)
                    message.Signal.WaitOne(2.Seconds());

                CalledSignal.Set();
            });
        }
    }
}
