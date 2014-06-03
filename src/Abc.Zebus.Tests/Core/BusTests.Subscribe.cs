using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Core;
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
        public void should_subscribe_to_message()
        {
            AddInvoker<FakeCommand>(shouldBeSubscribedOnStartup: true);
            AddInvoker<FakeRoutableCommand>(shouldBeSubscribedOnStartup: false);

            var subscriptions = new List<Subscription>();
            _directoryMock.CaptureEnumerable((IBus)_bus, (x, bus, items) => x.Update(bus, items), subscriptions);

            _bus.Start();
            _bus.Subscribe(Subscription.ByExample(x => new FakeRoutableCommand(1, "name")));
            subscriptions.Count.ShouldEqual(2);
            subscriptions.ShouldContain(new Subscription(MessageUtil.TypeId<FakeRoutableCommand>(), new BindingKey("1", "name", "*")));
        }

        [Test]
        public void can_batch_subscribe()
        {
            AddInvoker<FakeRoutableCommand>(shouldBeSubscribedOnStartup: false);

            var expectedSubscriptions = new List<Subscription>();
            _directoryMock.CaptureEnumerable((IBus)_bus, (x, bus, items) => x.Update(bus, items), expectedSubscriptions);

            _bus.Start();

            var subscriptions = new List<Subscription>();
            subscriptions.Add(Subscription.ByExample(x => new FakeRoutableCommand(1, "name")));
            subscriptions.Add(Subscription.ByExample(x => new FakeRoutableCommand(1, "toto")));
            subscriptions.Add(Subscription.ByExample(x => new FakeRoutableCommand(2, "name")));

            _bus.Subscribe(subscriptions.ToArray());
            expectedSubscriptions.Count.ShouldEqual(3);
            expectedSubscriptions.ShouldContain(new Subscription(MessageUtil.TypeId<FakeRoutableCommand>(), new BindingKey("1", "name", "*")));
            expectedSubscriptions.ShouldContain(new Subscription(MessageUtil.TypeId<FakeRoutableCommand>(), new BindingKey("1", "toto", "*")));
            expectedSubscriptions.ShouldContain(new Subscription(MessageUtil.TypeId<FakeRoutableCommand>(), new BindingKey("2", "name", "*")));
        }

        [Test]
        public void can_batch_and_dispose_empty_subscribe_list()
        {
            AddInvoker<FakeRoutableCommand>(shouldBeSubscribedOnStartup: false);


            _directoryMock.CaptureEnumerable((IBus)_bus, (x, bus, items) => x.Update(bus, items), new List<Subscription>());

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

            subscription.Dispose();

            _directoryMock.Verify(x => x.Update(_bus, It.Is<IEnumerable<Subscription>>(c => !c.Any())));
        }

        [Test]
        public void should_unsubscribe_to_message()
        {
            AddInvoker<FakeRoutableCommand>(shouldBeSubscribedOnStartup: false);

            _bus.Start();
            var subscription = _bus.Subscribe(Subscription.ByExample(x => new FakeRoutableCommand(1, "name")));

            subscription.Dispose();

            _directoryMock.Verify(x => x.Update(_bus, It.Is<IEnumerable<Subscription>>(c => !c.Any())));
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
    }
}