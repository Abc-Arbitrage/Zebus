using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Abc.Zebus.Util.Extensions
{
    internal static class ExtendTask
    {
        public static async Task WithTimeoutAsync(this Task task, TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (task.Status == TaskStatus.RanToCompletion)
                return;

            cancellationToken.ThrowIfCancellationRequested();
            if (await Task.WhenAny(task, Task.Delay(timeout, cancellationToken)).ConfigureAwait(false) != task)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException("Task timed out");
            }

            await task.ConfigureAwait(false);
        }

        public static async Task<T> WithTimeoutAsync<T>(this Task<T> task, TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (task.Status == TaskStatus.RanToCompletion)
                return task.Result;

            cancellationToken.ThrowIfCancellationRequested();
            if (await Task.WhenAny(task, Task.Delay(timeout, cancellationToken)).ConfigureAwait(false) != task)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException("Task timed out");
            }

            return await task.ConfigureAwait(false);
        }

        public static T WaitSync<T>(this Task<T> task)
        {
            try
            {
                return task.Result;
            }
            catch (AggregateException ex)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }
    }
}
