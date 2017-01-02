using System;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Dispatch.Pipes;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Dispatch;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Messages;
using Abc.Zebus.Util;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Dispatch
{
    [TestFixture]
    public class DispatchQueueTests
    {
        private DispatchQueue _dispatchQueue;
        private PipeManager _pipeManager;

        [SetUp]
        public void Setup()
        {
            _pipeManager = new PipeManager(new IPipeSource[0]);
            _dispatchQueue = new DispatchQueue(_pipeManager, "Default");
        }

        [Test]
        public void should_not_run_invoker_when_queue_is_not_started()
        {
            var started = new ManualResetEvent(false);
            EnqueueInvocation(x => started.Set());

            var executed = started.WaitOne(100.Milliseconds());
            executed.ShouldBeFalse();
        }


        [Test]
        public void should_run_invoker_when_queue_is_started()
        {
            var started = new ManualResetEvent(false);
            
            _dispatchQueue.Start();
            EnqueueInvocation(x => started.Set());
            
            var executed = started.WaitOne(500.Milliseconds());
            executed.ShouldBeTrue();
        }

        [Test]
        public void should_finish_current_invocation_before_stopping()
        {
            var currentInvokerStarted = new ManualResetEvent(false);
            var currentInvokerFinished = new ManualResetEvent(false);

            _dispatchQueue.Start();
            EnqueueInvocation(x =>
            {
                currentInvokerStarted.Set();
                currentInvokerFinished.WaitOne();
            });

            currentInvokerStarted.WaitOne(500.Milliseconds()).ShouldBeTrue();

            var stopTask = Task.Run(() => _dispatchQueue.Stop());
            Thread.Sleep(100);
            stopTask.IsCompleted.ShouldBeFalse();

            Task.Run(() => currentInvokerFinished.Set());
            stopTask.Wait(500.Milliseconds()).ShouldBeTrue();
        }

        private void EnqueueInvocation(Action<IMessageHandlerInvocation> callback)
        {
            var dispatch = new MessageDispatch(MessageContext.CreateTest(), new FakeEvent(0), (d, r) => { });
            var invoker = new TestMessageHandlerInvoker<FakeEvent> { InvokeMessageHandlerCallback = callback };

            _dispatchQueue.Enqueue(dispatch, invoker);
        }

        [Test]
        public void should_purge()
        {
            _dispatchQueue.Start();
            EnqueueInvocation(x => Thread.Sleep(400));
            EnqueueInvocation(x => Thread.Sleep(400));

            Wait.Until(() => _dispatchQueue.QueueLength == 1, 500.Milliseconds());

            _dispatchQueue.Purge();

            _dispatchQueue.QueueLength.ShouldEqual(0);
        }

        [Test, Timeout(1000)]
        public void should_restart()
        {
            _dispatchQueue.Start();
            _dispatchQueue.Stop();

            var started = new ManualResetEvent(false);
            EnqueueInvocation(x => started.Set());

            _dispatchQueue.Start();

            started.WaitOne();
            Assert.Pass();
        }
    }
}