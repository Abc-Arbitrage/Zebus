using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Directory;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Routing;
using Abc.Zebus.Scan;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Comparison;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Testing.UnitTesting;
using Abc.Zebus.Tests.Messages;
using Abc.Zebus.Util;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Core
{
    public partial class BusTests
    {
        public class Subscribe : BusTests
        {
            [Test]
            public void should_subscribe_to_message_for_all_binding_keys()
            {
                AddInvoker<FakeCommand>(shouldBeSubscribedOnStartup: false);
                AddInvoker<FakeRoutableCommand>(shouldBeSubscribedOnStartup: false);
                var subscriptions = new List<SubscriptionsForType>();
                _directoryMock.CaptureEnumerable((IBus)_bus, (x, bus, items) => x.UpdateSubscriptionsAsync(bus, items), subscriptions);

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
                _directoryMock.CaptureEnumerable((IBus)_bus, (x, bus, items) => x.UpdateSubscriptionsAsync(bus, items), subscriptions);

                _bus.Start();
                _bus.Subscribe(Subscription.ByExample(x => new FakeRoutableCommand(1, "name")));

                subscriptions.ExpectedSingle();
                subscriptions.ShouldContain(new SubscriptionsForType(MessageUtil.TypeId<FakeRoutableCommand>(), new BindingKey("1", "name", "*")));
            }

            [Test]
            public void should_resend_existing_bindings_when_making_a_new_subscription_to_a_type()
            {
                AddInvoker<FakeRoutableCommand>(shouldBeSubscribedOnStartup: false);
                _bus.Start();
                _bus.Subscribe(Subscription.ByExample(x => new FakeRoutableCommand(1, "firstRoutingValue")));
                var subscriptions = new List<SubscriptionsForType>();
                _directoryMock.CaptureEnumerable((IBus)_bus, (x, bus, items) => x.UpdateSubscriptionsAsync(bus, items), subscriptions);

                _bus.Subscribe(Subscription.ByExample(x => new FakeRoutableCommand(1, "secondRoutingValue")));

                subscriptions.Count.ShouldEqual(1);
                subscriptions.ShouldContain(new SubscriptionsForType(MessageUtil.TypeId<FakeRoutableCommand>(), new BindingKey("1", "firstRoutingValue", "*"), new BindingKey("1", "secondRoutingValue", "*")));
            }

            [Test]
            public void can_batch_subscribe()
            {
                AddInvoker<FakeRoutableCommand>(shouldBeSubscribedOnStartup: false);

                var directorySubscriptions = new List<SubscriptionsForType>();
                _directoryMock.CaptureEnumerable((IBus)_bus, (x, bus, items) => x.UpdateSubscriptionsAsync(bus, items), directorySubscriptions);

                _bus.Start();

                var subscriptions = new List<Subscription>();
                subscriptions.Add(Subscription.ByExample(x => new FakeRoutableCommand(1, "name")));
                subscriptions.Add(Subscription.ByExample(x => new FakeRoutableCommand(1, "toto")));
                subscriptions.Add(Subscription.ByExample(x => new FakeRoutableCommand(2, "name")));

                _bus.Subscribe(subscriptions.ToArray());
                directorySubscriptions.ExpectedSingle()
                                      .ShouldEqual(new SubscriptionsForType(MessageUtil.TypeId<FakeRoutableCommand>(),
                                                                            new BindingKey("1", "name", "*"),
                                                                            new BindingKey("1", "toto", "*"),
                                                                            new BindingKey("2", "name", "*")));
            }

            [Test]
            public async Task can_batch_and_dispose_empty_subscribe_list()
            {
                AddInvoker<FakeRoutableCommand>(shouldBeSubscribedOnStartup: false);

                _directoryMock.CaptureEnumerable((IBus)_bus, (x, bus, items) => x.UpdateSubscriptionsAsync(bus, items), new List<SubscriptionsForType>());

                _bus.Start();

                var subscriptions = new List<Subscription>();

                var subscription = _bus.Subscribe(subscriptions.ToArray());
                subscriptions.Count.ShouldEqual(0);

                subscription.Dispose();
                await _bus.WhenUnsubscribeCompletedAsync();
            }

            [Test]
            public async Task should_unsubscribe_to_batch()
            {
                AddInvoker<FakeRoutableCommand>(shouldBeSubscribedOnStartup: false);
                _bus.Start();
                var subscriptions = new List<Subscription>();
                subscriptions.Add(Subscription.ByExample(x => new FakeRoutableCommand(1, "name")));
                subscriptions.Add(Subscription.ByExample(x => new FakeRoutableCommand(1, "toto")));
                subscriptions.Add(Subscription.ByExample(x => new FakeRoutableCommand(2, "name")));
                var subscription = _bus.Subscribe(subscriptions.ToArray());
                var directorySubscriptions = new List<SubscriptionsForType>();
                _directoryMock.CaptureEnumerable((IBus)_bus, (x, bus, items) => x.UpdateSubscriptionsAsync(bus, items), directorySubscriptions);

                subscription.Dispose();
                await _bus.WhenUnsubscribeCompletedAsync();

                Wait.Until(() => directorySubscriptions.Count > 0, 2.Seconds());

                directorySubscriptions.ExpectedSingle().ShouldEqual(new SubscriptionsForType(MessageUtil.TypeId<FakeRoutableCommand>()));
            }

            [Test]
            public async Task should_not_unsubscribe_static_subscription()
            {
                AddInvoker<FakeRoutableCommand>(shouldBeSubscribedOnStartup: true);
                _bus.Start();
                var subscription = _bus.Subscribe(Subscription.ByExample(x => new FakeRoutableCommand(1, "name")));
                var directorySubscriptions = new List<SubscriptionsForType>();
                _directoryMock.CaptureEnumerable((IBus)_bus, (x, bus, items) => x.UpdateSubscriptionsAsync(bus, items), directorySubscriptions);

                subscription.Dispose();
                await _bus.WhenUnsubscribeCompletedAsync();

                Wait.Until(() => directorySubscriptions.Count > 0, 2.Seconds());

                directorySubscriptions.ExpectedSingle().ShouldEqual(new SubscriptionsForType(MessageUtil.TypeId<FakeRoutableCommand>(), BindingKey.Empty));
            }

            [Test]
            public async Task should_unsubscribe_from_message()
            {
                AddInvoker<FakeRoutableCommand>(shouldBeSubscribedOnStartup: false);
                _bus.Start();
                var subscription = _bus.Subscribe(Subscription.ByExample(x => new FakeRoutableCommand(1, "name")));
                var subscriptions = new List<SubscriptionsForType>();
                _directoryMock.CaptureEnumerable((IBus)_bus, (x, bus, items) => x.UpdateSubscriptionsAsync(bus, items), subscriptions);

                subscription.Dispose();
                await _bus.WhenUnsubscribeCompletedAsync();

                Wait.Until(() => subscriptions.Count > 0, 2.Seconds());

                subscriptions.ShouldContain(new SubscriptionsForType(MessageUtil.TypeId<FakeRoutableCommand>()));
            }

            [Test]
            public async Task should_not_resend_other_message_subscriptions_when_unsubscribing_from_a_message()
            {
                AddInvoker<FakeCommand>(shouldBeSubscribedOnStartup: false);
                AddInvoker<FakeRoutableCommand>(shouldBeSubscribedOnStartup: false);
                _bus.Start();
                var firstSubscription = _bus.Subscribe<FakeCommand>(cmd => { });
                var secondSubscription = _bus.Subscribe(Subscription.ByExample(x => new FakeRoutableCommand(1, "plop")));
                var subscriptions = new List<SubscriptionsForType>();
                _directoryMock.CaptureEnumerable((IBus)_bus, (x, bus, items) => x.UpdateSubscriptionsAsync(bus, items), subscriptions);

                firstSubscription.Dispose();
                await _bus.WhenUnsubscribeCompletedAsync();

                Wait.Until(() => subscriptions.Count > 0, 2.Seconds());

                subscriptions.ExpectedSingle();
                subscriptions.ShouldContain(new SubscriptionsForType(MessageUtil.TypeId<FakeCommand>()));
            }

            [Test]
            public async Task should_unsubscribe_when_last_subscription_is_disposed()
            {
                AddInvoker<FakeRoutableCommand>(shouldBeSubscribedOnStartup: false);

                var lastDirectorySubscriptions = new List<SubscriptionsForType>();

                _directoryMock.Setup(x => x.UpdateSubscriptionsAsync(_bus, It.IsAny<IEnumerable<SubscriptionsForType>>()))
                              .Callback<IBus, IEnumerable<SubscriptionsForType>>((bus, sub) => lastDirectorySubscriptions = sub.ToList())
                              .Returns(Task.CompletedTask);

                _bus.Start();

                var subscription1 = _bus.Subscribe(Subscription.ByExample(x => new FakeRoutableCommand(1, "name")));
                var subscription2 = _bus.Subscribe(Subscription.ByExample(x => new FakeRoutableCommand(1, "toto")));

                subscription1.Dispose();
                await _bus.WhenUnsubscribeCompletedAsync();

                Wait.Until(() => lastDirectorySubscriptions.Count > 0, 2.Seconds());
                lastDirectorySubscriptions.ExpectedSingle().BindingKeys.ShouldBeEquivalentTo(new[] { new BindingKey("1", "toto", "*") });

                lastDirectorySubscriptions.Clear();
                subscription2.Dispose();
                await _bus.WhenUnsubscribeCompletedAsync();

                Wait.Until(() => lastDirectorySubscriptions.Count > 0, 2.Seconds());
                lastDirectorySubscriptions.ExpectedSingle().BindingKeys.ShouldBeEmpty();
            }

            [Test]
            public void should_auto_subscribe()
            {
                AddInvoker<FakeCommand>(shouldBeSubscribedOnStartup: true);
                AddInvoker<FakeEvent>(shouldBeSubscribedOnStartup: false);

                var subscriptions = new List<Subscription>();

                _directoryMock.Setup(x => x.RegisterAsync(_bus, It.Is<Peer>(p => p.DeepCompare(_self)), It.IsAny<IEnumerable<Subscription>>()))
                              .Callback<IBus, Peer, IEnumerable<Subscription>>((x, y, items) => subscriptions.AddRange(items))
                              .Returns(Task.CompletedTask);

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

                _directoryMock.Setup(x => x.RegisterAsync(_bus, It.Is<Peer>(p => p.DeepCompare(_self)), It.IsAny<IEnumerable<Subscription>>()))
                              .Callback<IBus, Peer, IEnumerable<Subscription>>((x, y, items) => subscriptions.AddRange(items))
                              .Returns(Task.CompletedTask);

                _bus.Start();

                subscriptions.Count.ShouldEqual(1);
                subscriptions.Single().MessageTypeId.ShouldEqual(MessageUtil.TypeId<FakeCommand>());
                subscriptions.Single().BindingKey.ShouldEqual(BindingKey.Empty);
            }

            [Test]
            public void should_subscribe_with_dynamic_handler()
            {
                _bus.Start();

                var invokers = new List<IMessageHandlerInvoker>();
                _messageDispatcherMock.Setup(x => x.AddInvoker(It.IsAny<DynamicMessageHandlerInvoker>())).Callback((IMessageHandlerInvoker i) => invokers.Add(i));

                var handlerMock = new Mock<IMessageHandler<IMessage>>();
                var subscriptions = new[] { Subscription.Any<FakeEvent>() };
                _bus.Subscribe(subscriptions, handlerMock.Object.Handle);

                var invoker = invokers.ExpectedSingle();
                invoker.Mode.ShouldEqual(MessageHandlerInvokerMode.Synchronous);
                invoker.DispatchQueueName.ShouldEqual(DispatchQueueNameScanner.DefaultQueueName);
                invoker.MessageHandlerType.ShouldNotBeNull();
                invoker.MessageType.ShouldEqual(typeof(FakeEvent));
                invoker.MessageTypeId.ShouldEqual(new MessageTypeId(typeof(FakeEvent)));
            }

            [Test]
            public void should_subscribe_to_single_subscription_with_dynamic_handler()
            {
                _bus.Start();

                var invokers = new List<IMessageHandlerInvoker>();
                _messageDispatcherMock.Setup(x => x.AddInvoker(It.IsAny<DynamicMessageHandlerInvoker>())).Callback((IMessageHandlerInvoker i) => invokers.Add(i));

                var handlerMock = new Mock<IMessageHandler<IMessage>>();
                _bus.Subscribe(Subscription.Any<FakeEvent>(), handlerMock.Object.Handle);

                var invoker = invokers.ExpectedSingle();
                invoker.Mode.ShouldEqual(MessageHandlerInvokerMode.Synchronous);
                invoker.DispatchQueueName.ShouldEqual(DispatchQueueNameScanner.DefaultQueueName);
                invoker.MessageHandlerType.ShouldNotBeNull();
                invoker.MessageType.ShouldEqual(typeof(FakeEvent));
                invoker.MessageTypeId.ShouldEqual(new MessageTypeId(typeof(FakeEvent)));
            }

            [Test]
            public async Task should_unsubscribe_from_handler()
            {
                _bus.Start();

                var invokers = new List<IMessageHandlerInvoker>();
                _messageDispatcherMock.Setup(x => x.AddInvoker(It.IsAny<DynamicMessageHandlerInvoker>()))
                                      .Callback((IMessageHandlerInvoker i) => invokers.Add(i));

                _messageDispatcherMock.Setup(x => x.RemoveInvoker(It.IsAny<DynamicMessageHandlerInvoker>()))
                                      .Callback((IMessageHandlerInvoker i) => invokers.Remove(i));

                var subscription = _bus.Subscribe((FakeEvent e) => { });

                invokers.Count.ShouldEqual(1);

                subscription.Dispose();
                await _bus.WhenUnsubscribeCompletedAsync();

                invokers.Count.ShouldEqual(0);
            }

            [Test]
            public void should_not_keep_subscriptions_after_restart()
            {
                AddInvoker<FakeRoutableCommand>(shouldBeSubscribedOnStartup: false);

                var subscriptions = new List<Subscription>();
                _directoryMock.Setup(dir => dir.RegisterAsync(It.IsAny<IBus>(), It.IsAny<Peer>(), It.IsAny<IEnumerable<Subscription>>()))
                              .Callback((IBus bus, Peer peer, IEnumerable<Subscription> subs) => subscriptions = subs.ToList())
                              .Returns(Task.CompletedTask);

                _bus.Start();
                _bus.Subscribe(Subscription.ByExample(x => new FakeRoutableCommand(1, "name")));

                _bus.Stop();
                _bus.Start();

                subscriptions.Count.ShouldEqual(0);
            }

            [Test]
            public void should_only_call_specific_handler_once_per_type()
            {
                _bus.Start();
                _bus.Subscribe(new[]
                               {
                                   Subscription.Matching<FakeRoutableCommand>(x => x.Id == 1),
                                   Subscription.Matching<FakeRoutableCommand>(x => x.Id == 2),
                                   Subscription.Any<FakeCommand>()
                               },
                               message => { });

                _messageDispatcherMock.Verify(x => x.AddInvoker(It.IsAny<IMessageHandlerInvoker>()), Times.Exactly(2));
                _messageDispatcherMock.Verify(x => x.AddInvoker(It.Is<IMessageHandlerInvoker>(invoker => invoker.MessageType == typeof(FakeRoutableCommand))), Times.Once);
                _messageDispatcherMock.Verify(x => x.AddInvoker(It.Is<IMessageHandlerInvoker>(invoker => invoker.MessageType == typeof(FakeCommand))), Times.Once);
            }

            [Test]
            public async Task should_wait_for_unsubscribe_to_complete_before_adding_new_subscriptions()
            {
                _bus.Start();

                _directoryMock.Setup(i => i.UpdateSubscriptionsAsync(It.IsAny<IBus>(), It.IsAny<IEnumerable<SubscriptionsForType>>()))
                              .Returns(Task.CompletedTask);

                var subA = await _bus.SubscribeAsync(Subscription.Any<FakeCommand>(), msg => { }).ConfigureAwait(true);

                var unsubscribeTcs = new TaskCompletionSource<object>();
                var unsubscribeSent = false;
                _directoryMock.Setup(i => i.UpdateSubscriptionsAsync(It.IsAny<IBus>(), It.IsAny<IEnumerable<SubscriptionsForType>>()))
                              .Callback(() => unsubscribeSent = true)
                              .Returns(unsubscribeTcs.Task);

                subA.Dispose();
                Wait.Until(() => unsubscribeSent, 2.Seconds());

                var newSubscribeSent = false;
                _directoryMock.Setup(i => i.UpdateSubscriptionsAsync(It.IsAny<IBus>(), It.IsAny<IEnumerable<SubscriptionsForType>>()))
                              .Callback(() => newSubscribeSent = true)
                              .Returns(Task.CompletedTask);

                var subBTask = _bus.SubscribeAsync(Subscription.Any<FakeCommand>(), msg => { });

                Thread.Sleep(200);
                newSubscribeSent.ShouldBeFalse();

                unsubscribeTcs.SetResult(null);
                Wait.Until(() => newSubscribeSent, 2.Seconds());

                await subBTask.ConfigureAwait(true);
            }

            [Test]
            [Explicit("The implementation is non trivial and will be dealt with later")]
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

            [Test]
            public async Task should_batch_multiple_subscription_requests()
            {
                AddInvoker<FakeRoutableCommand>(shouldBeSubscribedOnStartup: false);

                var subscriptions = new List<SubscriptionsForType>();
                _directoryMock.CaptureEnumerable((IBus)_bus, (x, bus, items) => x.UpdateSubscriptionsAsync(bus, items), subscriptions);

                _bus.Start();

                var batch = new SubscriptionRequestBatch();

                var requestA = new SubscriptionRequest(new[]
                {
                    Subscription.ByExample(x => new FakeRoutableCommand(1, "foo")),
                    Subscription.ByExample(x => new FakeRoutableCommand(1, "bar"))
                });

                var requestB = new SubscriptionRequest(new[]
                {
                    Subscription.ByExample(x => new FakeRoutableCommand(1, "bar")),
                    Subscription.ByExample(x => new FakeRoutableCommand(1, "baz"))
                });

                requestA.AddToBatch(batch);
                requestB.AddToBatch(batch);

                var taskA = _bus.SubscribeAsync(requestA);
                var taskB = _bus.SubscribeAsync(requestB);

                subscriptions.ShouldBeEmpty();

                await batch.SubmitAsync();

                await Task.WhenAll(taskA, taskB);

                subscriptions.ExpectedSingle()
                             .ShouldEqual(new SubscriptionsForType(
                                              MessageUtil.TypeId<FakeRoutableCommand>(),
                                              new BindingKey("1", "foo", "*"),
                                              new BindingKey("1", "bar", "*"),
                                              new BindingKey("1", "baz", "*")
                                          )
                             );

                subscriptions.Clear();

                taskA.Result.Dispose();
                await _bus.WhenUnsubscribeCompletedAsync();

                subscriptions.ExpectedSingle()
                             .ShouldEqual(new SubscriptionsForType(
                                              MessageUtil.TypeId<FakeRoutableCommand>(),
                                              new BindingKey("1", "bar", "*"),
                                              new BindingKey("1", "baz", "*")
                                          )
                             );
            }

            [Test]
            public void empty_batch_should_not_block()
            {
                var batch = new SubscriptionRequestBatch();
                batch.SubmitAsync().IsCompleted.ShouldBeTrue();
            }

            [Test]
            public async Task should_not_unsubscribe_when_bus_is_stopped()
            {
                AddInvoker<FakeCommand>(shouldBeSubscribedOnStartup: false);

                _bus.Start();
                var subscription = _bus.Subscribe(Subscription.Any<FakeCommand>());

                _bus.Stop();
                _directoryMock.Invocations.Clear();

                subscription.Dispose();
                await _bus.WhenUnsubscribeCompletedAsync();

                _directoryMock.Verify(i => i.UpdateSubscriptionsAsync(_bus, It.IsAny<IEnumerable<SubscriptionsForType>>()), Times.Never);
            }

            [Test]
            public void should_throw_when_batch_is_sent_after_bus_is_stopped()
            {
                AddInvoker<FakeCommand>(shouldBeSubscribedOnStartup: false);

                var batch = new SubscriptionRequestBatch();
                var request = new SubscriptionRequest(Subscription.Any<FakeCommand>());
                request.AddToBatch(batch);

                _bus.Start();
                var _ = _bus.SubscribeAsync(request);

                _bus.Stop();

                var submitTask = batch.SubmitAsync();
                Assert.Throws<AggregateException>(() => submitTask.Wait()).InnerExceptions.ExpectedSingle().ShouldBe<InvalidOperationException>();
            }

            [Test]
            public void should_not_subscribe_when_bus_is_stopped()
            {
                AddInvoker<FakeCommand>(shouldBeSubscribedOnStartup: false);

                Assert.Throws<InvalidOperationException>(() => _bus.Subscribe(Subscription.Any<FakeCommand>()));
            }

            [Test]
            public async Task should_not_subscribe_after_bus_is_stopped()
            {
                AddInvoker<FakeCommand>(shouldBeSubscribedOnStartup: false);

                _bus.Start();
                var subscription = _bus.Subscribe(Subscription.Any<FakeCommand>());

                _bus.Stop();
                _bus.Start();
                _directoryMock.Invocations.Clear();

                subscription.Dispose();
                await _bus.WhenUnsubscribeCompletedAsync();

                _directoryMock.Verify(i => i.UpdateSubscriptionsAsync(_bus, It.IsAny<IEnumerable<SubscriptionsForType>>()), Times.Never);
            }

            private void SendParallelSubscriptionUpdates(int threadCount, int subscriptionCountPerThread)
            {
                var subscriptionVersion = 0;
                var subscribertasks = Enumerable.Range(0, threadCount).Select(ite => Task.Run(async () =>
                {
                    for (var i = 0; i < subscriptionCountPerThread; ++i)
                    {
                        var currentVersion = Interlocked.Increment(ref subscriptionVersion);
                        await _bus.SubscribeAsync(new SubscriptionRequest(new[] { new Subscription(MessageUtil.TypeId<FakeRoutableCommand>(), new BindingKey(currentVersion.ToString(), string.Empty)) })
                        {
                            ThereIsNoHandlerButIKnowWhatIAmDoing = true
                        }).ConfigureAwait(false);
                    }
                })).ToArray();

                Task.WaitAll(subscribertasks);
            }

            private void CaptureHighestVersionOnUpdate(ConcurrentQueue<int> highestSubscriptionVersionOnEachUpdate)
            {
                _directoryMock.Setup(dir => dir.UpdateSubscriptionsAsync(It.IsAny<IBus>(), It.IsAny<IEnumerable<SubscriptionsForType>>())).Callback(
                    (IBus bus, IEnumerable<SubscriptionsForType> subs) =>
                    {
                        var highestSubscriptionVersionNumber = subs.SelectMany(x => x.BindingKeys).Select(x => int.Parse(x.GetPart(0))).OrderBy(x => x).Last();
                        highestSubscriptionVersionOnEachUpdate.Enqueue(highestSubscriptionVersionNumber);
                    }).Returns(Task.CompletedTask);
            }
        }
    }
}
