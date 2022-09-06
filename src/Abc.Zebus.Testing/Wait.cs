using System;
using System.Diagnostics;
using System.Threading;
using Abc.Zebus.Util;
using JetBrains.Annotations;

namespace Abc.Zebus.Testing
{
    internal static class Wait
    {
        public static void Until([InstantHandle] Func<bool> exitCondition, int timeoutInSeconds)
            => Until(exitCondition, timeoutInSeconds.Seconds(), () => string.Empty);

        public static void Until([InstantHandle] Func<bool> exitCondition, TimeSpan timeout, string? message = null)
            => Until(exitCondition, timeout, () => message ?? "Timed out");

        public static void Until([InstantHandle] Func<bool> exitCondition, TimeSpan timeout, Func<string?> message)
        {
            var sw = Stopwatch.StartNew();

            while (true)
            {
                if (exitCondition())
                    break;

                if (sw.Elapsed > timeout)
                    throw new TimeoutException(message?.Invoke() ?? "Timed out");

                Thread.Sleep(10);
            }
        }
    }
}
