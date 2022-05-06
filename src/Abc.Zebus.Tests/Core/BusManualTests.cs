using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Core;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Routing;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util;
using Moq;
using NUnit.Framework;
using ProtoBuf;

namespace Abc.Zebus.Tests.Core
{
    [TestFixture]
    [Explicit]
    [Category("ManualOnly")]
    public class BusManualTests
    {
        // this must be a valid directory endpoint
        private static readonly string _directoryEndPoint = Environment.GetEnvironmentVariable("ZEBUS_TEST_DIRECTORY");
        private static readonly string _environment = Environment.GetEnvironmentVariable("ZEBUS_TEST_ENVIRONMENT");

        [Test]
        public void StartBusWithoutStop()
        {
            CreateBusFactory().CreateAndStartBus();
        }

        [Test]
        public void StartAndStopBus()
        {
            var bus = CreateBusFactory().CreateAndStartBus();

            Thread.Sleep(1000);

            bus.Stop();
        }

        [Test]
        public void PublishEventWithoutLocalDispatch()
        {
            using (var bus = CreateBusFactory().WithHandlers(typeof(ManualEventHandler)).CreateAndStartBus())
            {
                using (LocalDispatch.Disable())
                {
                    bus.Publish(new ManualEvent(42));
                }

                Wait.Until(() => ManualEventHandler.ReceivedEventCount == 1, 300.Seconds());
            }
        }

        [Test]
        public void SendCommandWithoutLocalDispatch()
        {
            using (var bus = CreateBusFactory().WithHandlers(typeof(ManualCommandHandler)).CreateAndStartBus())
            {
                using (LocalDispatch.Disable())
                {
                    bus.Send(new ManualCommand(42)).Wait();
                }

                Console.WriteLine(ManualCommandHandler.LastId);
            }
        }

        [Test]
        public void SubscribeToRoutableEventWithIds()
        {
            using var bus = CreateBusFactory().CreateAndStartBus();

            var values = new List<string>();

            var subscriptions = new[]
            {
                Subscription.Matching<RoutableEventWithIds>(x => x.Ids.Contains(42)),
                Subscription.Matching<RoutableEventWithIds>(x => x.Ids.Contains(43)),
            };

            bus.Subscribe(subscriptions, x => values.Add(((RoutableEventWithIds)x).Value));

            Thread.Sleep(1000);

            using (LocalDispatch.Disable())
            {
                bus.Publish(new RoutableEventWithIds { Ids = new[] { 1, 2 }, Value = "1" });
                bus.Publish(new RoutableEventWithIds { Ids = new[] { 1, 2, 42 }, Value = "2" });
                bus.Publish(new RoutableEventWithIds { Ids = new[] { 1, 2, 42, 43 }, Value = "3" });
                bus.Publish(new RoutableEventWithIds { Ids = new[] { 1, 2, 43 }, Value = "4" });
            }

            Thread.Sleep(1000);

            values.ShouldEqualDeeply(new List<string> { "2", "3", "4" });
        }

        [Test]
        public void SendSleepCommands()
        {
            var tasks = new List<Task>();
            using (var bus = CreateBusFactory().WithHandlers(typeof(SleepCommandHandler)).CreateAndStartBus())
            {
                for (var i = 0; i < 20; ++i)
                {
                    tasks.Add(bus.Send(new SleepCommand()));
                }

                Task.WaitAll(tasks.ToArray());
            }
        }

        [Test]
        public void should_generate_unacked_messages()
        {
            var targetConfig = new BusConfiguration(_directoryEndPoint)
            {
                IsPersistent = true,
            };

            var target = CreateBusFactory().WithHandlers(typeof(ManualEventHandler))
                                           .WithConfiguration(targetConfig, "Dev")
                                           .WithPeerId("Some.Random.Persistent.Peer.0")
                                           .CreateAndStartBus();
            using (var source = CreateBusFactory().CreateAndStartBus())
            {
                source.Publish(new ManualEvent(42));
                Thread.Sleep(2000);

                target.Dispose();

                for (int i = 0; i < 1_000; i++)
                {
                    source.Publish(new ManualEvent(42));
                }
            }
        }

        private static BusFactory CreateBusFactory()
        {
            return new BusFactory().WithConfiguration(_directoryEndPoint, _environment);
        }

        [ProtoContract]
        public class ManualEvent : IEvent
        {
            [ProtoMember(1, IsRequired = true)]
            public readonly int Id;

            public ManualEvent(int id)
            {
                Id = id;
            }
        }

        [ProtoContract]
        public class ManualCommand : ICommand
        {
            [ProtoMember(1, IsRequired = true)]
            public readonly int Id;

            public ManualCommand(int id)
            {
                Id = id;
            }
        }

        public class ManualEventHandler : IMessageHandler<ManualEvent>
        {
            public static int ReceivedEventCount;

            public void Handle(ManualEvent message)
            {
                Interlocked.Increment(ref ReceivedEventCount);

                Console.WriteLine(message.Id);
            }
        }

        public class ManualCommandHandler : IMessageHandler<ManualCommand>
        {
            public static int LastId;

            public void Handle(ManualCommand message)
            {
                LastId = message.Id;
            }
        }

        public class SleepCommand : ICommand
        {
        }

        public class SleepCommandHandler : IMessageHandler<SleepCommand>
        {
            public void Handle(SleepCommand message)
            {
                Thread.Sleep(1000);
            }
        }

        [ProtoContract]
        [Routable]
        public class RoutableEventWithIds : IEvent
        {
            [ProtoMember(1)]
            [RoutingPosition(1)]
            public int[] Ids { get; set; }

            [ProtoMember(2)]
            public string Value { get; set; }
        }
    }
}
