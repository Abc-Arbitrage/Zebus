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
using Abc.Zebus.Tests.Dispatch.DispatchMessages;
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
            var message = new ExecutableEvent();
            EnqueueInvocation(message);

            Thread.Sleep(200);

            message.HandleStarted.IsSet.ShouldBeFalse();
        }

        [Test]
        public void should_run_invoker_when_queue_is_started()
        {
            var message = new ExecutableEvent();

            _dispatchQueue.Start();
            EnqueueInvocation(message);

            message.HandleStarted.Wait(500.Milliseconds()).ShouldBeTrue();
        }

        [Test]
        public void should_run_continuation()
        {
            _dispatchQueue.Start();

            var task = EnqueueInvocation(new ExecutableEvent());

            task.Wait(500.Milliseconds()).ShouldBeTrue();
        }

        [Test]
        public void should_finish_current_invocation_before_stopping()
        {
            var message = new ExecutableEvent { IsBlocking = true };

            _dispatchQueue.Start();
            EnqueueInvocation(message);

            message.HandleStarted.Wait(500.Milliseconds()).ShouldBeTrue();

            var stopTask = Task.Run(() => _dispatchQueue.Stop());
            Thread.Sleep(100);
            stopTask.IsCompleted.ShouldBeFalse();

            Task.Run(() => message.Unblock());
            stopTask.Wait(500.Milliseconds()).ShouldBeTrue();
        }

        [Test]
        public void should_purge()
        {
            _dispatchQueue.Start();

            var message = new ExecutableEvent { IsBlocking = true };
            EnqueueInvocation(message);

            message.HandleStarted.Wait(500.Milliseconds()).ShouldBeTrue();

            EnqueueInvocation(new ExecutableEvent());
            EnqueueInvocation(new ExecutableEvent());

            _dispatchQueue.QueueLength.ShouldEqual(2);

            _dispatchQueue.Purge();

            _dispatchQueue.QueueLength.ShouldEqual(0);

            message.Unblock();
        }

        [Test]
        public void should_restart()
        {
            _dispatchQueue.Start();
            _dispatchQueue.Stop();

            var message = new ExecutableEvent { IsBlocking = true };
            EnqueueInvocation(message);

            _dispatchQueue.Start();

            message.HandleStarted.Wait(500.Milliseconds()).ShouldBeTrue();
        }

        [Test, Repeat(5)]
        public void should_stop_processing_messages_after_stop()
        {
            var firstMessage = new ExecutableEvent { IsBlocking = true };
            EnqueueInvocation(firstMessage);

            var otherMessageTasks = new List<Task>();
            otherMessageTasks.Add(EnqueueInvocation(new ExecutableEvent { IsBlocking = true }));
            otherMessageTasks.Add(EnqueueInvocation(new ExecutableEvent { IsBlocking = true }));

            _dispatchQueue.Start();

            Thread.Sleep(50);

            otherMessageTasks.Add(EnqueueInvocation(new ExecutableEvent { IsBlocking = true }));
            otherMessageTasks.Add(EnqueueInvocation(new ExecutableEvent { IsBlocking = true }));

            var stopTask = Task.Run(() => _dispatchQueue.Stop());

            Wait.Until(() => _dispatchQueue.IsRunning == false, 500.Milliseconds());

            firstMessage.Unblock();

            stopTask.Wait(500.Milliseconds()).ShouldBeTrue();

            foreach (var otherMessageTask in otherMessageTasks)
            {
                otherMessageTask.IsCompleted.ShouldBeFalse();
            }
        }

        [Test, Repeat(5)]
        public void should_batch_messages()
        {
            _dispatchQueue.Start();

            var message0 = new ExecutableEvent { IsBlocking = true };
            EnqueueInvocation(message0);
            message0.HandleStarted.Wait(500.Milliseconds()).ShouldBeTrue();

            var invokedBatches = new List<List<IMessage>>();

            var messages = Enumerable.Range(0, 10)
                                     .Select(x => new ExecutableEvent { Callback = i => invokedBatches.Add(i.Messages.ToList()) })
                                     .ToList();

            foreach (var message in messages)
            {
                EnqueueBatchedInvocation(message);
            }

            message0.Unblock();

            Wait.Until(() => invokedBatches.Count >= 1, 500.Milliseconds());

            var invokedBatch = invokedBatches.ExpectedSingle();
            invokedBatch.ShouldBeEquivalentTo(messages);
        }

        [Test]
        public void should_run_continuation_with_batch()
        {
            _dispatchQueue.Start();

            var firstMessage = new ExecutableEvent { IsBlocking = true };
            EnqueueInvocation(firstMessage);

            var dispatch1 = EnqueueBatchedInvocation(new ExecutableEvent());
            var dispatch2 = EnqueueBatchedInvocation(new ExecutableEvent());

            firstMessage.Unblock();

            dispatch1.Wait(500.Milliseconds()).ShouldBeTrue();
            dispatch2.Wait(500.Milliseconds()).ShouldBeTrue();
        }

        [Test]
        public void should_run_continuation_with_batch_error()
        {
            _dispatchQueue.Start();

            var firstMessage = new ExecutableEvent { IsBlocking = true };
            EnqueueInvocation(firstMessage);

            var dispatch1 = EnqueueBatchedInvocation(new ExecutableEvent { Callback = x => Throw() });
            var dispatch2 = EnqueueBatchedInvocation(new ExecutableEvent());

            firstMessage.Unblock();

            dispatch1.Wait(500.Milliseconds()).ShouldBeTrue();
            dispatch1.Result.Errors.ShouldNotBeEmpty();
            dispatch2.Wait(500.Milliseconds()).ShouldBeTrue();
            dispatch2.Result.Errors.ShouldNotBeEmpty();
        }

        [Test, Timeout(5000)]
        public void should_run_async_without_blocking_dispatcher_thread()
        {
            var firstMessage = new ExecutableEvent { IsBlocking = true };
            var secondMessage = new ExecutableEvent { Callback = _ => firstMessage.Unblock() };

            var invoker = new TestAsyncMessageHandlerInvoker<ExecutableEvent>();

            var firstCompletion = new TaskCompletionSource<DispatchResult>();
            var secondCompletion = new TaskCompletionSource<DispatchResult>();

            var firstDispatch = new MessageDispatch(MessageContext.CreateTest(), firstMessage, (d, r) => firstCompletion.SetResult(r));
            firstDispatch.SetHandlerCount(1);

            var secondDispatch = new MessageDispatch(MessageContext.CreateTest(), secondMessage, (d, r) => secondCompletion.SetResult(r));
            secondDispatch.SetHandlerCount(1);

            _dispatchQueue.RunAsync(firstDispatch, invoker);
            _dispatchQueue.RunAsync(secondDispatch, invoker);

            Task.WhenAll(firstCompletion.Task, secondCompletion.Task).Wait(2000.Milliseconds()).ShouldBeTrue();
        }

        private static void Throw()
        {
            throw new Exception("Test");
        }

        private Task<DispatchResult> EnqueueInvocation(ExecutableEvent message)
        {
            var tcs = new TaskCompletionSource<DispatchResult>();

            var dispatch = new MessageDispatch(MessageContext.CreateTest(), message, (d, r) => tcs.SetResult(r));
            dispatch.SetHandlerCount(1);

            var invoker = new TestMessageHandlerInvoker<ExecutableEvent>();

            _dispatchQueue.Enqueue(dispatch, invoker);

            return tcs.Task;
        }

        private Task<DispatchResult> EnqueueBatchedInvocation(ExecutableEvent message)
        {
            var tcs = new TaskCompletionSource<DispatchResult>();

            var dispatch = new MessageDispatch(MessageContext.CreateTest(), message, (d, r) => tcs.SetResult(r));
            dispatch.SetHandlerCount(1);

            var invoker = new TestBatchedMessageHandlerInvoker<FakeEvent>();

            _dispatchQueue.Enqueue(dispatch, invoker);

            return tcs.Task;
        }
    }
}
