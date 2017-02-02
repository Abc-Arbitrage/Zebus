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
        [TestCase(true)]
        [TestCase(false)]
        public void should_finish_async_invocations_before_stopping(bool captureContext)
        {
            var tcs = new TaskCompletionSource<object>();

            var message = new AsyncExecutableEvent { Callback = async _ => await tcs.Task.ConfigureAwait(captureContext) };

            _dispatchQueue.Start();
            var invocation = EnqueueAsyncInvocation(message);

            message.HandleStarted.Wait(500.Milliseconds()).ShouldBeTrue();

            var stopTask = Task.Run(() => _dispatchQueue.Stop());
            Thread.Sleep(100);
            stopTask.IsCompleted.ShouldBeFalse();

            Task.Run(() => tcs.SetResult(null));
            invocation.Wait(500.Milliseconds()).ShouldBeTrue();
            stopTask.Wait(500.Milliseconds()).ShouldBeTrue();
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void should_not_accept_invocations_after_stop_while_completing_async_invocations(bool captureContext)
        {
            var tcs = new TaskCompletionSource<object>();

            var message = new AsyncExecutableEvent { Callback = async _ => await tcs.Task.ConfigureAwait(captureContext) };

            _dispatchQueue.Start();
            EnqueueAsyncInvocation(message);

            message.HandleStarted.Wait(500.Milliseconds()).ShouldBeTrue();

            var stopTask = Task.Run(() => _dispatchQueue.Stop());
            Thread.Sleep(100);
            stopTask.IsCompleted.ShouldBeFalse();

            var afterStopMessage = new ExecutableEvent();
            EnqueueInvocation(afterStopMessage);
            Thread.Sleep(100);

            Task.Run(() => tcs.SetResult(null));
            stopTask.Wait(500.Milliseconds()).ShouldBeTrue();

            afterStopMessage.HandleStarted.IsSet.ShouldBeFalse();
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
            var tcs = new TaskCompletionSource<object>();

            var firstMessage = new AsyncExecutableEvent
            {
                Callback = async _ => await tcs.Task.ConfigureAwait(true)
            };

            var secondMessage = new AsyncExecutableEvent
            {
                Callback = _ =>
                {
                    tcs.SetResult(null);
                    return TaskUtil.Completed;
                }
            };

            _dispatchQueue.Start();
            var firstTask = EnqueueAsyncInvocation(firstMessage);
            var secondTask = EnqueueAsyncInvocation(secondMessage);

            Task.WhenAll(firstTask, secondTask).Wait(2000.Milliseconds()).ShouldBeTrue();

            firstMessage.DispatchQueueName.ShouldEqual(_dispatchQueue.Name);
            secondMessage.DispatchQueueName.ShouldEqual(_dispatchQueue.Name);
        }

        [Test, Timeout(5000)]
        public void should_enqueue_async_continuations_on_dispatch_queue_when_requested()
        {
            _dispatchQueue.Start();

            var tcs = new TaskCompletionSource<object>();

            string firstMessageDispatchQueueBeforeAwait = null;
            string firstMessageDispatchQueueAfterAwait = null;
            string secondMessageDispatchQueueBeforeAwait = null;
            string secondMessageDispatchQueueAfterAwait = null;

            var firstTask = EnqueueAsyncInvocation(new AsyncExecutableEvent
            {
                Callback = async _ =>
                {
                    firstMessageDispatchQueueBeforeAwait = DispatchQueue.GetCurrentDispatchQueueName();
                    await tcs.Task.ConfigureAwait(true);
                    firstMessageDispatchQueueAfterAwait = DispatchQueue.GetCurrentDispatchQueueName();
                }
            });

            var secondTask = EnqueueAsyncInvocation(new AsyncExecutableEvent
            {
                Callback = async _ =>
                {
                    secondMessageDispatchQueueBeforeAwait = DispatchQueue.GetCurrentDispatchQueueName();
                    await tcs.Task.ConfigureAwait(false);
                    secondMessageDispatchQueueAfterAwait = DispatchQueue.GetCurrentDispatchQueueName();
                }
            });

            EnqueueInvocation(new ExecutableEvent { Callback = x => tcs.SetResult(null) });

            Task.WhenAll(firstTask, secondTask).Wait(2000.Milliseconds()).ShouldBeTrue();

            Volatile.Read(ref firstMessageDispatchQueueBeforeAwait).ShouldEqual(_dispatchQueue.Name);
            Volatile.Read(ref secondMessageDispatchQueueBeforeAwait).ShouldEqual(_dispatchQueue.Name);

            Volatile.Read(ref firstMessageDispatchQueueAfterAwait).ShouldEqual(_dispatchQueue.Name);
            Volatile.Read(ref secondMessageDispatchQueueAfterAwait).ShouldBeNull();
        }

        [Test, Timeout(5000)]
        public void should_propagate_async_exceptions_from_continuations()
        {
            var tcs = new TaskCompletionSource<object>();

            _dispatchQueue.Start();

            var firstTask = EnqueueAsyncInvocation(new AsyncExecutableEvent
            {
                Callback = async _ =>
                {
                    await tcs.Task.ConfigureAwait(true);
                    throw new InvalidOperationException("First");
                }
            });

            var secondTask = EnqueueAsyncInvocation(new AsyncExecutableEvent
            {
                Callback = async _ =>
                {
                    await tcs.Task.ConfigureAwait(false);
                    throw new InvalidOperationException("Second");
                }
            });

            EnqueueInvocation(new ExecutableEvent { Callback = x => tcs.SetResult(null) });

            Task.WhenAll(firstTask, secondTask).Wait(2000.Milliseconds()).ShouldBeTrue();

            firstTask.Result.Errors.Count.ShouldEqual(1);
            secondTask.Result.Errors.Count.ShouldEqual(1);
        }

        [Test, Timeout(5000)]
        public void should_internleave_sync_and_async_messages_properly()
        {
            var tcs = new TaskCompletionSource<object>();

            _dispatchQueue.Start();

            var sequence = new List<int>();
            Action<int> addSequence = i =>
            {
                lock (sequence)
                {
                    sequence.Add(i);
                }
            };

            var firstTask = EnqueueAsyncInvocation(new AsyncExecutableEvent
            {
                Callback = async _ =>
                {
                    addSequence(1);
                    await tcs.Task.ConfigureAwait(true);
                    addSequence(4);
                }
            });

            var secondTask = EnqueueAsyncInvocation(new AsyncExecutableEvent
            {
                Callback = async _ =>
                {
                    addSequence(2);
                    await tcs.Task.ConfigureAwait(true);
                    addSequence(5);
                }
            });

            var thirdTask = EnqueueInvocation(new ExecutableEvent
            {
                Callback = _ =>
                {
                    addSequence(3);
                    Task.Run(() => tcs.SetResult(null));
                }
            });

            Task.WhenAll(firstTask, secondTask, thirdTask).Wait(2000.Milliseconds()).ShouldBeTrue();

            lock (sequence)
            {
                sequence.ShouldEqual(Enumerable.Range(1, 5));
            }
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

        private Task<DispatchResult> EnqueueAsyncInvocation(AsyncExecutableEvent message)
        {
            var tcs = new TaskCompletionSource<DispatchResult>();

            var dispatch = new MessageDispatch(MessageContext.CreateTest(), message, (d, r) => tcs.SetResult(r));
            dispatch.SetHandlerCount(1);

            var invoker = new TestAsyncMessageHandlerInvoker<AsyncExecutableEvent>();

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
