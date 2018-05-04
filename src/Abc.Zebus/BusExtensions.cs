using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Util.Extensions;

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

        public static Task Send<T>(this IBus bus, IEnumerable<T> commands, Action<ICommand, int> onCommandExecuted)
            where T : ICommand
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

        public static Task<IDisposable> SubscribeAsync(this IBus bus, Subscription subscription)
            => bus.SubscribeAsync(new SubscriptionRequest(subscription));

        public static Task<IDisposable> SubscribeAsync(this IBus bus, IEnumerable<Subscription> subscriptions)
            => bus.SubscribeAsync(new SubscriptionRequest(subscriptions));

        public static Task<IDisposable> SubscribeAsync(this IBus bus, Subscription subscription, Action<IMessage> handler)
            => bus.SubscribeAsync(new SubscriptionRequest(subscription), handler);

        public static Task<IDisposable> SubscribeAsync(this IBus bus, IEnumerable<Subscription> subscriptions, Action<IMessage> handler)
            => bus.SubscribeAsync(new SubscriptionRequest(subscriptions), handler);

        public static IDisposable Subscribe(this IBus bus, Subscription subscription)
            => SubscribeAsync(bus, subscription).WaitSync();

        public static IDisposable Subscribe(this IBus bus, IEnumerable<Subscription> subscriptions)
            => SubscribeAsync(bus, subscriptions).WaitSync();

        public static IDisposable Subscribe<T>(this IBus bus, Action<T> handler)
            where T : class, IMessage
            => Subscribe(bus, Subscription.Any<T>(), msg => handler((T)msg));

        public static IDisposable Subscribe(this IBus bus, Subscription subscription, Action<IMessage> handler)
            => SubscribeAsync(bus, subscription, handler).WaitSync();

        public static IDisposable Subscribe(this IBus bus, IEnumerable<Subscription> subscriptions, Action<IMessage> handler)
            => SubscribeAsync(bus, subscriptions, handler).WaitSync();
    }
}
