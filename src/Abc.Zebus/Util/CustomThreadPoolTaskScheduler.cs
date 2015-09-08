// Copyright 2011 Olivier Deheurles
// https://github.com/disruptor-net/Disruptor-net
// Licensed under the Apache License, Version 2.0 (the "License");

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Abc.Zebus.Util
{
    /// <summary>
    ///     An implementation of <see cref="TaskScheduler" /> which creates an underlying thread pool and set processor affinity to each thread.
    /// </summary>
    internal sealed class CustomThreadPoolTaskScheduler : TaskScheduler, IDisposable
    {
        [ThreadStatic]
        private static bool _isThreadPoolThread;

        private BlockingCollection<Task> _tasks;
        private List<Thread> _threads;

        public int TaskCount => _tasks.Count;

        /// <summary>
        ///     Indicates the maximum concurrency level this <see cref="T:System.Threading.Tasks.TaskScheduler" /> is able to support.
        /// </summary>
        /// <returns>
        ///     Returns an integer that represents the maximum concurrency level.
        /// </returns>
        public override int MaximumConcurrencyLevel => _threads.Count;

        /// <summary>
        ///     Create a new <see cref="CustomThreadPoolTaskScheduler" /> with a provided number of background threads.
        /// </summary>
        /// <param name="numberOfThreads">Total number of threads in the pool.</param>
        /// <param name="baseThreadName"></param>
        public CustomThreadPoolTaskScheduler(int numberOfThreads, string baseThreadName = null)
        {
            if (numberOfThreads < 1)
                throw new ArgumentOutOfRangeException(nameof(numberOfThreads));

            CreateThreads(numberOfThreads, baseThreadName);
        }

        /// <summary>
        ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            if (_tasks != null)
            {
                _tasks.CompleteAdding();

                _threads.ForEach(t => t.Join());

                _tasks.Dispose();
                _tasks = null;
            }
        }

        /// <summary>
        ///     Generates an enumerable of <see cref="T:System.Threading.Tasks.Task" /> instances currently queued to the scheduler waiting to be executed.
        /// </summary>
        /// <returns>
        ///     An enumerable that allows traversal of tasks currently queued to this scheduler.
        /// </returns>
        /// <exception cref="T:System.NotSupportedException">This scheduler is unable to generate a list of queued tasks at this time.</exception>
        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return _tasks.ToArray();
        }


        /// <summary>
        ///     Queues a <see cref="T:System.Threading.Tasks.Task" /> to the scheduler.
        /// </summary>
        /// <param name="task">
        ///     The <see cref="T:System.Threading.Tasks.Task" /> to be queued.
        /// </param>
        /// <exception cref="T:System.ArgumentNullException">
        ///     The <paramref name="task" /> argument is null.
        /// </exception>
        protected override void QueueTask(Task task)
        {
            _tasks.Add(task);
        }

        /// <summary>
        ///     Determines whether the provided <see cref="T:System.Threading.Tasks.Task" /> can be executed synchronously in this call, and if it can, executes it.
        /// </summary>
        /// <returns>
        ///     A Boolean value indicating whether the task was executed inline.
        /// </returns>
        /// <param name="task">
        ///     The <see cref="T:System.Threading.Tasks.Task" /> to be executed.
        /// </param>
        /// <param name="taskWasPreviouslyQueued">A Boolean denoting whether or not task has previously been queued. If this parameter is True, then the task may have been previously queued (scheduled); if False, then the task is known not to have been queued, and this call is being made in order to execute the task inline without queuing it.</param>
        /// <exception cref="T:System.ArgumentNullException">
        ///     The <paramref name="task" /> argument is null.
        /// </exception>
        /// <exception cref="T:System.InvalidOperationException">
        ///     The <paramref name="task" /> was already executed.
        /// </exception>
        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            if (!_isThreadPoolThread)
                return false;

            if (taskWasPreviouslyQueued)
                TryDequeue(task);

            return TryExecuteTask(task);
        }

        private void CreateThreads(int numberOfThreads, string baseThreadName)
        {
            _tasks = new BlockingCollection<Task>();

            _threads = Enumerable.Range(0, numberOfThreads)
                                 .Select(threadIndex => CreateThread(baseThreadName, threadIndex))
                                 .ToList();

            _threads.ForEach(t => t.Start());
        }

        private Thread CreateThread(string baseThreadName, int threadIndex)
        {
            var thread = new Thread(ThreadProc) { IsBackground = true };
            if (baseThreadName != null)
                thread.Name = baseThreadName + "." + threadIndex;

            return thread;
        }

        [DebuggerStepThrough]
        private void ThreadProc()
        {
            _isThreadPoolThread = true;
            foreach (var t in _tasks.GetConsumingEnumerable())
            {
                TryExecuteTask(t);
            }
        }
    }
}