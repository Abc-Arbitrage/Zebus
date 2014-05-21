using System;
using System.Linq.Expressions;
using System.Threading;
using Abc.Zebus.Util;
using Abc.Zebus.Util.Annotations;

namespace Abc.Zebus.Testing
{
    internal static class Wait
    {
        public static void Until([InstantHandle] Func<bool> exitCondition, int timeoutInSeconds)
        {
            Until(exitCondition, timeoutInSeconds.Seconds(), () => string.Empty);
        }

        public static void Until(Expression<Func<bool>> exitCondition, TimeSpan timeout, string message = null)
        {
            Until(exitCondition.Compile(), timeout, () => message ?? exitCondition.ToString());
        }

        public static void Until([InstantHandle] Func<bool> exitCondition, TimeSpan timeout, Func<string> message)
        {
             var now = DateTime.UtcNow;
             var elapsedTime = now.Add(timeout);
             while (true)
             {
                 if (now >= elapsedTime)
                    throw new TimeoutException(message());

                 if (exitCondition())
                     break;

                Thread.Sleep(50);
                now = DateTime.UtcNow;
             }
         }
    }
}