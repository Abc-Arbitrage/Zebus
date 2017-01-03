using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Dispatch.Pipes;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Dispatch;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Messages;
using Abc.Zebus.Util;
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
            _dispatchQueue = new DispatchQueue(_pipeManager, 200, "Default");
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

        [Test]
        public void should_purge()
        {
            _dispatchQueue.Start();

            var message1HandleStarted = false;
            EnqueueInvocation(x =>
            {
                message1HandleStarted = true;
                Thread.Sleep(300);
            });

            Wait.Until(() => message1HandleStarted, 500.Milliseconds());

            EnqueueInvocation(x => { });
            EnqueueInvocation(x => { });

            _dispatchQueue.QueueLength.ShouldEqual(2);

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
        
        [Test]
        public void should_batch_messages()
        {
            _dispatchQueue.Start();

            var firstHandlerSignal = new ManualResetEvent(false);
            EnqueueInvocation(x => firstHandlerSignal.WaitOne());

            var callParameters = new List<List<FakeEvent>>();
            Action<List<FakeEvent>> callback = messages => callParameters.Add(messages);

            EnqueueBatchedInvocation(callback);
            EnqueueBatchedInvocation(callback);
            EnqueueBatchedInvocation(callback);

            firstHandlerSignal.Set();
            
            Wait.Until(() => callParameters.Count >= 1, 50000.Milliseconds());

            callParameters.Count.ShouldEqual(1);
            callParameters[0].Count.ShouldEqual(3);
        }

        [Test]
        public void should_run_continuation_with_batch()
        {
            _dispatchQueue.Start();

            var firstHandlerSignal = new ManualResetEvent(false);
            EnqueueInvocation(x => firstHandlerSignal.WaitOne());

            var dispatch1 = EnqueueBatchedInvocation(null);
            var dispatch2 = EnqueueBatchedInvocation(null);

            firstHandlerSignal.Set();

            dispatch1.Wait(50000.Milliseconds()).ShouldBeTrue();
            dispatch2.Wait(50000.Milliseconds()).ShouldBeTrue();
        }

        [Test]
        public void should_run_continuation_with_batch_error()
        {
            _dispatchQueue.Start();

            var firstHandlerSignal = new ManualResetEvent(false);
            EnqueueInvocation(x => firstHandlerSignal.WaitOne());

            var dispatch1 = EnqueueBatchedInvocation(x => Throw());
            var dispatch2 = EnqueueBatchedInvocation(x => Throw());

            firstHandlerSignal.Set();

            Wait.Until(() => dispatch2.IsFaulted, 500.Milliseconds());
            Wait.Until(() => dispatch1.IsFaulted, 500.Milliseconds());
        }

        private static void Throw()
        {
            throw new Exception("Test");
        }

        private void EnqueueInvocation(Action<IMessageHandlerInvocation> callback)
        {
            var dispatch = new MessageDispatch(MessageContext.CreateTest(), new FakeEvent(0), (d, r) => { });
            dispatch.SetHandlerCount(1);

            var invoker = new TestMessageHandlerInvoker<FakeEvent> { InvokeMessageHandlerCallback = callback };

            _dispatchQueue.Enqueue(dispatch, invoker);
        }

        private Task<DispatchResult> EnqueueBatchedInvocation(Action<List<FakeEvent>> callback)
        {
            var taskcompletionSource = new TaskCompletionSource<DispatchResult>();

            var dispatch = new MessageDispatch(MessageContext.CreateTest(), new FakeEvent(0), (d, r) =>
            {
                if (r.Errors.Any())
                    taskcompletionSource.SetException(r.Errors);
                else
                    taskcompletionSource.SetResult(r);
            });
            dispatch.SetHandlerCount(1);

            var invoker = new TestBatchedMessageHandlerInvoker<FakeEvent> { HandlerCallback = callback };

            _dispatchQueue.Enqueue(dispatch, invoker);

            return taskcompletionSource.Task;
        }
    }
}