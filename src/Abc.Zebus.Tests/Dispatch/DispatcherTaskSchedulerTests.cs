using System;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Dispatch
{
    [TestFixture]
    public class DispatcherTaskSchedulerTests
    {
        private DispatcherTaskScheduler _taskScheduler;

        [SetUp]
        public void Setup()
        {
            _taskScheduler = new DispatcherTaskScheduler();
        }

        [Test]
        public void should_not_start_processing_thread()
        {
            var started = new ManualResetEvent(false);
            StartTask(() => started.Set());

            var executed = started.WaitOne(100.Milliseconds());
            executed.ShouldBeFalse();
        }

        [Test, Timeout(1000)]
        public void should_start_processing_thread()
        {
            var started = new ManualResetEvent(false);
            StartTask(() => started.Set());

            _taskScheduler.Start();

            started.WaitOne();
            Assert.Pass();
        }


        [Test]
        public void should_finish_current_task_before_stopping()
        {
            _taskScheduler.Start();

            var started = new ManualResetEvent(false);
            var canStop = new ManualResetEvent(false);
            StartTask(() =>
            {
                started.Set();
                canStop.WaitOne();
            });

            started.WaitOne(100.Milliseconds()).ShouldBeTrue();

            var stoppingSchedulerTask = Task.Factory.StartNew(() => _taskScheduler.Stop());
            Task.Factory.StartNew(() => canStop.Set());

            stoppingSchedulerTask.Wait(100.Milliseconds()).ShouldBeTrue();
        }

        [Test]
        public void should_clear_enqueued_tasks()
        {
            StartTask(() => {});
            StartTask(() => {});

            _taskScheduler.TaskCount.ShouldEqual(2);

            _taskScheduler.ClearTasks();

            _taskScheduler.TaskCount.ShouldEqual(0);
        }

        [Test]
        public void should_purge_tasks()
        {
            _taskScheduler.Start();
            StartTask(() => Thread.Sleep(1000));
            StartTask(() => Thread.Sleep(1000));

            Wait.Until(() => _taskScheduler.TaskCount == 1, 1.Second());

            _taskScheduler.PurgeTasks();

            _taskScheduler.TaskCount.ShouldEqual(0);
        }

        [Test]
        public void should_throw_exception_if_trying_to_clear_when_started()
        {
            _taskScheduler.Start();
            Assert.Throws<InvalidOperationException>(() => _taskScheduler.ClearTasks());
        }

        [Test, Timeout(1000)]
        public void should_restart()
        {
            _taskScheduler.Start();
            _taskScheduler.Stop();

            var started = new ManualResetEvent(false);
            StartTask(() => started.Set());

            _taskScheduler.Start();

            started.WaitOne();
            Assert.Pass();
        }
        
        private void StartTask(Action action)
        {
            Task.Factory.StartNew(action, new CancellationToken(), TaskCreationOptions.None, _taskScheduler);
        }

    }
}