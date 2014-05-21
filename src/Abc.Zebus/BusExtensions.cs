using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Dispatch;

namespace Abc.Zebus
{
    public static class BusExtensions
    {
        public static Task<CommandResult> SendWithoutLocalDispatch(this IBus bus, ICommand command)
        {
            using (LocalDispatch.Disable())
            {
                return bus.Send(command);
            }
        }

        public static void PublishWithoutLocalDispatch(this IBus bus, IEvent @event)
        {
            using (LocalDispatch.Disable())
            {
                bus.Publish(@event);
            }
        }

        public static Task Send(this IBus bus, IEnumerable<ICommand> commands)
        {
            var sendTasks = commands.Select(bus.Send);
            return Task.WhenAll(sendTasks);
        }

        public static Task Send<T>(this IBus bus, IEnumerable<T> commands, Action<ICommand, int> onCommandExecuted) where T : ICommand
        {
            var sendTasks = commands.Select(command => bus.Send(command).ContinueWith(task => onCommandExecuted(command, task.Result.ErrorCode), TaskContinuationOptions.ExecuteSynchronously));

            return Task.WhenAll(sendTasks);
        }

        public static Task<bool> SendMany(this IBus bus, IEnumerable<ICommand> commands, Action<ICommand, CommandResult> onCommandExecuted = null)
        {
            var sendTasks = commands.Select(command =>
            {
                var sendTask = bus.Send(command);
                if (onCommandExecuted != null)
                    sendTask.ContinueWith(task => onCommandExecuted(command, task.Result));

                return sendTask;
            });

            return Task.WhenAll(sendTasks).ContinueWith(t => t.Result.All(x => x.IsSuccess));
        }
    }
}