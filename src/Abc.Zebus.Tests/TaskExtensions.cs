using System.Threading.Tasks;
using Abc.Zebus.Testing;
using Abc.Zebus.Util;

namespace Abc.Zebus.Tests
{
    internal static class TaskExtensions
    {
        public static T WaitForActivation<T>(this T task)
            where T : Task
        {
            Wait.Until(
                () =>
                {
                    switch (task.Status)
                    {
                        case TaskStatus.Created:
                        case TaskStatus.WaitingToRun:
                        case TaskStatus.WaitingForActivation:
                            return false;

                        default:
                            return true;
                    }
                },
                30.Seconds()
            );

            return task;
        }
    }
}
