using System;
using System.Threading.Tasks;

namespace Abc.Zebus.Util
{
    internal static class TaskUtil
    {
        public static readonly Task Completed = Task.FromResult((object)null);

        public static Task FromError(Exception exception)
        {
            var tcs = new TaskCompletionSource<object>();
            tcs.SetException(exception);
            return tcs.Task;
        }
    }
}