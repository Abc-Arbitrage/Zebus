using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Abc.Zebus.Util.Extensions;

internal static class ExtendTask
{
    public static Task<Exception?> CaptureException(this Task task)
    {
        return task.ContinueWith(x => x.Exception != null && x.Exception.InnerExceptions.Count == 1 ? x.Exception.InnerExceptions.First() : x.Exception);
    }

    public static async Task WithTimeoutAsync(this Task task, TimeSpan timeout, CancellationToken cancellationToken = default)
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

    public static async Task<T> WithTimeoutAsync<T>(this Task<T> task, TimeSpan timeout, CancellationToken cancellationToken = default)
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
            if (ex.InnerException != null)
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();

            throw;
        }
    }
}
