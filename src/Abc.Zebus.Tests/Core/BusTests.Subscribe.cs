using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Core;
using Abc.Zebus.Directory;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Routing;
using Abc.Zebus.Scan;
using Abc.Zebus.Testing.Comparison;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Testing.UnitTesting;
using Abc.Zebus.Tests.Messages;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Core
{
    public partial class BusTests
    {
        [Test]
        public void should_subscribe_to_message_for_all_binding_keys()
        {
            AddInvoker<FakeCommand>(shouldBeSubscribedOnStartup: false);
            AddInvoker<FakeRoutableCommand>(shouldBeSubscribedOnStartup: false);
            var subscriptions = new List<SubscriptionsForType>();
            _directoryMock.CaptureEnumerable((IBus)_bus, (x, bus, items) => x.UpdateSubscriptions(bus, items), subscriptions);

            _bus.Start();
            _bus.Subscribe(Subscription.Any<FakeCommand>());

            subscriptions.ExpectedSingle();
            subscriptions.ShouldContain(new SubscriptionsForType(MessageUtil.TypeId<FakeCommand>(), BindingKey.Empty));
        }

        [Test]
        public void should_subscribe_to_message_but_not_resend_existing_subscriptions()
        {
            AddInvoker<FakeCommand>(shouldBeSubscribedOnStartup: true);
            AddInvoker<FakeRoutableCommand>(shouldBeSubscribedOnStartup: false);
            var subscriptions = new List<SubscriptionsForType>();
            _directoryMock.CaptureEnumerable((IBus)_bus, (x, bus, items) => x.UpdateSubscriptions(bus, items), subscriptions);

            _bus.Start();
            _bus.Subscribe(Subscription.ByExample(x => new FakeRoutableCommand(1, "name")));

            subscriptions.ExpectedSingle();
            subscriptions.ShouldContain(new SubscriptionsForType(MessageUtil.TypeId<FakeRoutableCommand>(), new BindingKey("1", "name", "*")));
        }

        [Test]
        public void should_resend_existing_bindings_when_making_a_new_subscription_to_a_type()
        {
            AddInvoker<FakeRoutableCommand>(shouldBeSubscribedOnStartup: false);
            _bus.Subscribe(Subscription.ByExample(x => new FakeRoutableCommand(1, "firstRoutingValue")));
            var subscriptions = new List<SubscriptionsForType>();
            _directoryMock.CaptureEnumerable((IBus)_bus, (x, bus, items) => x.UpdateSubscriptions(bus, items), subscriptions);
            _bus.Start();

            _bus.Subscribe(Subscription.ByExample(x => new FakeRoutableCommand(1, "secondRoutingValue")));

            subscriptions.Count.ShouldEqual(1);
            subscriptions.ShouldContain(new SubscriptionsForType(MessageUtil.TypeId<FakeRoutableCommand>(), new BindingKey("1", "firstRoutingValue", "*"), new BindingKey("1", "secondRoutingValue", "*")));
        }

        [Test]
        public void can_batch_subscribe()
        {
            AddInvoker<FakeRoutableCommand>(shouldBeSubscribedOnStartup: false);

            var directorySubscriptions = new List<SubscriptionsForType>();
            _directoryMock.CaptureEnumerable((IBus)_bus, (x, bus, items) => x.UpdateSubscriptions(bus, items), directorySubscriptions);

            _bus.Start();

            var subscriptions = new List<Subscription>();
            subscriptions.Add(Subscription.ByExample(x => new FakeRoutableCommand(1, "name")));
            subscriptions.Add(Subscription.ByExample(x => new FakeRoutableCommand(1, "toto")));
            subscriptions.Add(Subscription.ByExample(x => new FakeRoutableCommand(2, "name")));

            _bus.Subscribe(subscriptions.ToArray());
            directorySubscriptions.ExpectedSingle()
                                  .ShouldEqual(new SubscriptionsForType(MessageUtil.TypeId<FakeRoutableCommand>(), new BindingKey("1", "name", "*"),
                                                                                                                   new BindingKey("1", "toto", "*"),
                                                                                                                   new BindingKey("2", "name", "*")));
        }

        [Test]
        public void can_batch_and_dispose_empty_subscribe_list()
        {
            AddInvoker<FakeRoutableCommand>(shouldBeSubscribedOnStartup: false);


            _directoryMock.CaptureEnumerable((IBus)_bus, (x, bus, items) => x.UpdateSubscriptions(bus, items), new List<SubscriptionsForType>());

            _bus.Start();

            var subscriptions = new List<Subscription>();

            var subscription = _bus.Subscribe(subscriptions.ToArray());
            subscriptions.Count.ShouldEqual(0);

            subscription.Dispose();
        }

        [Test]
        public void should_unsubscribe_to_batch()
        {
            AddInvoker<FakeRoutableCommand>(shouldBeSubscribedOnStartup: false);
            _bus.Start();
            var subscriptions = new List<Subscription>();
            subscriptions.Add(Subscription.ByExample(x => new FakeRoutableCommand(1, "name")));
            subscriptions.Add(Subscription.ByExample(x => new FakeRoutableCommand(1, "toto")));
            subscriptions.Add(Subscription.ByExample(x => new FakeRoutableCommand(2, "name")));
            var subscription = _bus.Subscribe(subscriptions.ToArray());
            var directorySubscriptions = new List<SubscriptionsForType>();
            _directoryMock.CaptureEnumerable((IBus)_bus, (x, bus, items) => x.UpdateSubscriptions(bus, items), directorySubscriptions);

            subscription.Dispose();

            directorySubscriptions.ExpectedSingle().ShouldEqual(new SubscriptionsForType(MessageUtil.TypeId<FakeRoutableCommand>()));
        }

        [Test]
        public void should_not_unsubscribe_static_subscription()
        {
            AddInvoker<FakeRoutableCommand>(shouldBeSubscribedOnStartup: true);
            _bus.Start();
            var subscription = _bus.Subscribe(Subscription.ByExample(x => new FakeRoutableCommand(1, "name")));
            var directorySubscriptions = new List<SubscriptionsForType>();
            _directoryMock.CaptureEnumerable((IBus)_bus, (x, bus, items) => x.UpdateSubscriptions(bus, items), directorySubscriptions);

            subscription.Dispose();

            directorySubscriptions.ExpectedSingle().ShouldEqual(new SubscriptionsForType(MessageUtil.TypeId<FakeRoutableCommand>(), BindingKey.Empty));
        }

        [Test]
        public void should_unsubscribe_from_message()
        {
            AddInvoker<FakeRoutableCommand>(shouldBeSubscribedOnStartup: false);
            _bus.Start();
            var subscription = _bus.Subscribe(Subscription.ByExample(x => new FakeRoutableCommand(1, "name")));
            var subscriptions = new List<SubscriptionsForType>();
            _directoryMock.CaptureEnumerable((IBus)_bus, (x, bus, items) => x.UpdateSubscriptions(bus, items), subscriptions);

            subscription.Dispose();

            subscriptions.ShouldContain(new SubscriptionsForType(MessageUtil.TypeId<FakeRoutableCommand>()));
        }

        [Test]
        public void should_not_resend_other_message_subscriptions_when_unsubscribing_from_a_message()
        {
            AddInvoker<FakeCommand>(shouldBeSubscribedOnStartup: false);
            AddInvoker<FakeRoutableCommand>(shouldBeSubscribedOnStartup: false);
            _bus.Start();
            var firstSubscription = _bus.Subscribe<FakeCommand>(cmd => {});
            var secondSubscription = _bus.Subscribe(Subscription.ByExample(x => new FakeRoutableCommand(1, "plop")));
            var subscriptions = new List<SubscriptionsForType>();
            _directoryMock.CaptureEnumerable((IBus)_bus, (x, bus, items) => x.UpdateSubscriptions(bus, items), subscriptions);

            firstSubscription.Dispose();

            subscriptions.ExpectedSingle();
            subscriptions.ShouldContain(new SubscriptionsForType(MessageUtil.TypeId<FakeCommand>()));
        }

        [Test]
        public void should_unsubscribe_when_last_subscription_is_disposed()
        {
            AddInvoker<FakeRoutableCommand>(shouldBeSubscribedOnStartup: false);

            var lastDirectorySubscriptions = new List<SubscriptionsForType>();
            
            _directoryMock.Setup(x => x.UpdateSubscriptions(_bus, It.IsAny<IEnumerable<SubscriptionsForType>>()))
                          .Callback<IBus, IEnumerable<SubscriptionsForType>>((bus, sub) => lastDirectorySubscriptions = sub.ToList());

            _bus.Start();

            var subscription1 = _bus.Subscribe(Subscription.ByExample(x => new FakeRoutableCommand(1, "name")));
            var subscription2 = _bus.Subscribe(Subscription.ByExample(x => new FakeRoutableCommand(1, "toto")));

            subscription1.Dispose();
            lastDirectorySubscriptions.ExpectedSingle().BindingKeys.ShouldBeEquivalentTo(new[] { new BindingKey("1", "toto", "*") });

            subscription2.Dispose();
            lastDirectorySubscriptions.ExpectedSingle().BindingKeys.ShouldBeEmpty();
        }

        [Test]
        public void should_auto_subscribe()
        {
            AddInvoker<FakeCommand>(shouldBeSubscribedOnStartup: true);
            AddInvoker<FakeEvent>(shouldBeSubscribedOnStartup: false);

            var subscriptions = new List<Subscription>();

            _directoryMock.Setup(x => x.Register(_bus, It.Is<Peer>(p => p.DeepCompare(_self)), It.IsAny<IEnumerable<Subscription>>()))
                          .Callback<IBus, Peer, IEnumerable<Subscription>>((x, y, items) => subscriptions.AddRange(items));

            _bus.Start();

            subscriptions.Count.ShouldEqual(1);
            subscriptions.Single().MessageTypeId.ShouldEqual(MessageUtil.TypeId<FakeCommand>());
            subscriptions.Single().BindingKey.ShouldEqual(BindingKey.Empty);
        }

        [Test]
        public void should_not_auto_subscribe_twice_to_messages_with_multiple_handlers()
        {
            AddInvoker<FakeCommand>(shouldBeSubscribedOnStartup: true);
            AddInvoker<FakeCommand>(shouldBeSubscribedOnStartup: true);

            var subscriptions = new List<Subscription>();

            _directoryMock.Setup(x => x.Register(_bus, It.Is<Peer>(p => p.DeepCompare(_self)), It.IsAny<IEnumerable<Subscription>>()))
                          .Callback<IBus, Peer, IEnumerable<Subscription>>((x, y, items) => subscriptions.AddRange(items));

            _bus.Start();

            subscriptions.Count.ShouldEqual(1);
            subscriptions.Single().MessageTypeId.ShouldEqual(MessageUtil.TypeId<FakeCommand>());
            subscriptions.Single().BindingKey.ShouldEqual(BindingKey.Empty);
        }

        [Test]
        public void should_subscribe_with_handler()
        {
            _bus.Start();

            var invokers = new List<IMessageHandlerInvoker>();
            _messageDispatcherMock.Setup(x=>x.AddInvoker(It.IsAny<EventHandlerInvoker<FakeEvent>>())).Callback((IMessageHandlerInvoker i) => invokers.Add(i));

            _bus.Subscribe((FakeEvent e) => { });

            invokers.Count.ShouldEqual(1);
            invokers[0].CanInvokeSynchronously.ShouldBeTrue();
            invokers[0].DispatchQueueName.ShouldEqual(DispatchQueueNameScanner.DefaultQueueName);
            invokers[0].MessageHandlerType.ShouldNotBeNull();
            invokers[0].MessageType.ShouldEqual(typeof(FakeEvent));
            invokers[0].MessageTypeId.ShouldEqual(new MessageTypeId(typeof(FakeEvent)));
            invokers[0].ShouldBeSubscribedOnStartup.ShouldBeFalse();
            invokers[0].ShouldCreateStartedTasks.ShouldBeFalse();
        }

        [Test]
        public void should_subscribe_with_untype_handler()
        {
            _bus.Start();

            var invokers = new List<IMessageHandlerInvoker>();
            _messageDispatcherMock.Setup(x => x.AddInvoker(It.IsAny<EventHandlerInvoker>())).Callback((IMessageHandlerInvoker i) => invokers.Add(i));

            var handlerMock = new Mock<IMessageHandler<IMessage>>();
            var subscriptions = new[] { Subscription.Any<FakeEvent>() };
            _bus.Subscribe(subscriptions, handlerMock.Object.Handle);

            invokers.Count.ShouldEqual(1);
            invokers[0].CanInvokeSynchronously.ShouldBeTrue();
            invokers[0].DispatchQueueName.ShouldEqual(DispatchQueueNameScanner.DefaultQueueName);
            invokers[0].MessageHandlerType.ShouldNotBeNull();
            invokers[0].MessageType.ShouldEqual(typeof(FakeEvent));
            invokers[0].MessageTypeId.ShouldEqual(new MessageTypeId(typeof(FakeEvent)));
            invokers[0].ShouldCreateStartedTasks.ShouldBeFalse();
        }

        [Test]
        public void should_subscribe_to_single_subscription_with_untype_handler()
        {
            _bus.Start();

            var invokers = new List<IMessageHandlerInvoker>();
            _messageDispatcherMock.Setup(x => x.AddInvoker(It.IsAny<EventHandlerInvoker>())).Callback((IMessageHandlerInvoker i) => invokers.Add(i));

            var handlerMock = new Mock<IMessageHandler<IMessage>>();
            _bus.Subscribe(Subscription.Any<FakeEvent>(), handlerMock.Object.Handle);

            invokers.Count.ShouldEqual(1);
            invokers[0].CanInvokeSynchronously.ShouldBeTrue();
            invokers[0].DispatchQueueName.ShouldEqual(DispatchQueueNameScanner.DefaultQueueName);
            invokers[0].MessageHandlerType.ShouldNotBeNull();
            invokers[0].MessageType.ShouldEqual(typeof(FakeEvent));
            invokers[0].MessageTypeId.ShouldEqual(new MessageTypeId(typeof(FakeEvent)));
            invokers[0].ShouldCreateStartedTasks.ShouldBeFalse();
        }

        [Test]
        public void should_unsubscribe_from_handler()
        {
            _bus.Start();

            var invokers = new List<IMessageHandlerInvoker>();
            _messageDispatcherMock.Setup(x => x.AddInvoker(It.IsAny<EventHandlerInvoker<FakeEvent>>()))
                                  .Callback((IMessageHandlerInvoker i) => invokers.Add(i));

            _messageDispatcherMock.Setup(x => x.RemoveInvoker(It.IsAny<EventHandlerInvoker<FakeEvent>>()))
                                  .Callback((IMessageHandlerInvoker i) => invokers.Remove(i));

            var subscription = _bus.Subscribe((FakeEvent e) => { });

            invokers.Count.ShouldEqual(1);

            subscription.Dispose();

            invokers.Count.ShouldEqual(0);
        }

        [Test]
        public void should_not_keep_subscriptions_after_restart()
        {
            AddInvoker<FakeRoutableCommand>(shouldBeSubscribedOnStartup: false);

            var subscriptions = new List<Subscription>();
            _directoryMock.Setup(dir => dir.Register(It.IsAny<IBus>(), It.IsAny<Peer>(), It.IsAny<IEnumerable<Subscription>>()))
                          .Callback((IBus Bus, Peer peer, IEnumerable<Subscription> subs) => subscriptions = subs.ToList());
            _bus.Start();
            _bus.Subscribe(Subscription.ByExample(x => new FakeRoutableCommand(1, "name")));

            _bus.Stop();
            _bus.Start();
            
            subscriptions.Count.ShouldEqual(0);
        }

        [Test]
        [Ignore("The implementation is non trivial and will be dealt with later")]
        public void subscriptions_sent_to_the_directory_should_always_be_more_recent_than_the_previous()
        {
            const int threadCount = 10;
            const int subscriptionCountPerThread = 100;

            var highestSubscriptionVersionOnEachUpdate = new ConcurrentQueue<int>();
            CaptureHighestVersionOnUpdate(highestSubscriptionVersionOnEachUpdate);

            SendParallelSubscriptionUpdates(threadCount, subscriptionCountPerThread);

            var callOrder = highestSubscriptionVersionOnEachUpdate.ToList();
            for (var i = 0; i < highestSubscriptionVersionOnEachUpdate.Count - 1; i++)
                callOrder[i].ShouldBeLessOrEqualThan(callOrder[i + 1]);
        }

        private void SendParallelSubscriptionUpdates(int threadCount, int subscriptionCountPerThread)
        {
            var subscriptionVersion = 0;
            var subscribertasks = Enumerable.Range(0, threadCount).Select(ite => Task.Factory.StartNew(() =>
            {
                for (var i = 0; i < subscriptionCountPerThread; ++i)
                {
                    var currentVersion = Interlocked.Increment(ref subscriptionVersion);
                    _bus.Subscribe(new[] { new Subscription(MessageUtil.TypeId<FakeRoutableCommand>(), new BindingKey(currentVersion.ToString(), string.Empty)) }, SubscriptionOptions.ThereIsNoHandlerButIKnowWhatIAmDoing);
                }
            })).ToArray();

            Task.WaitAll(subscribertasks);
        }

        private void CaptureHighestVersionOnUpdate(ConcurrentQueue<int> highestSubscriptionVersionOnEachUpdate)
        {
            _directoryMock.Setup(dir => dir.UpdateSubscriptions(It.IsAny<IBus>(), It.IsAny<IEnumerable<SubscriptionsForType>>())).Callback(
                (IBus bus, IEnumerable<SubscriptionsForType> subs) =>
                {
                    var highestSubscriptionVersionNumber = subs.SelectMany(x => x.BindingKeys).Select(x => int.Parse(x.GetPart(0))).OrderBy(x => x).Last();
                    highestSubscriptionVersionOnEachUpdate.Enqueue(highestSubscriptionVersionNumber);
                });
        }
    }
}