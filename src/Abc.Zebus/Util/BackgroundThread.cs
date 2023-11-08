using System;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Abc.Zebus.Util;

internal static class BackgroundThread
{
    private static readonly ILogger _logger = ZebusLogManager.GetLogger(typeof(BackgroundThread));

    public static Thread Start(ThreadStart startAction, Action? abortAction = null)
    {
        var thread = new Thread(Wrapper(startAction, abortAction))
        {
            IsBackground = true
        };

        thread.Start();
        return thread;
    }

    public static Thread Start<T>(Action<T> startAction, T state, Action? abortAction = null)
    {
        var thread = new Thread(Wrapper(startAction, abortAction))
        {
            IsBackground = true
        };

        thread.Start(state);
        return thread;
    }

    private static void SafeAbort(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running action");
        }
    }

    private static ParameterizedThreadStart Wrapper<T>(Action<T> action, Action? abortAction)
    {
        return s =>
        {
            try
            {
                action((T)s!);
            }
            catch (ThreadAbortException ex)
            {
                if (abortAction != null)
                    SafeAbort(abortAction);

                (ex.ExceptionState as EventWaitHandle)?.Set();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running action");
            }
        };
    }

    private static ThreadStart Wrapper(ThreadStart action, Action? abortAction)
    {
        return () =>
        {
            try
            {
                action();
            }
            catch (ThreadAbortException ex)
            {
                if (abortAction != null)
                    SafeAbort(abortAction);

                (ex.ExceptionState as EventWaitHandle)?.Set();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running action");
            }
        };
    }
}
