using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using ABC.ServiceBus.Contracts;
using Abc.Zebus.Core;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util;
using NUnit.Framework;
using ProtoBuf;
using Timeout = ABC.ServiceBus.Contracts.Timeout;

namespace Abc.Zebus.Tests.Core
{
    [TestFixture, Ignore("Manual test")]
    public class BusManualTests
    {
        [Test]
        public void SendSeveralTimeoutRequestAndCheckForResponseUnicity()
        {
            var handler = new AccumulatingTimeoutCommandHandler();

            var bus = CreateBusFactory() 
                .WithHandlers(typeof(AccumulatingTimeoutCommandHandler))
                .ConfigureContainer(x => x.ForSingletonOf<AccumulatingTimeoutCommandHandler>().Use(handler))
                .CreateAndStartBus();

            bus.Subscribe(Subscription.Matching<TimeoutCommand>(x => x.ServiceName == bus.PeerId.ToString()));

            const int messageCount = 100;

            for (var i = 0; i < messageCount; i++)
            {
                var state = new State { Value = i };
                var requestTimeoutCommand = Timeout.BuildRequest(Guid.NewGuid().ToString(), DateTime.Now.AddSeconds(1), state, bus.PeerId.ToString());
                bus.Send(requestTimeoutCommand);
            }

            Thread.Sleep(2.Seconds());

            handler.Commands.Count.ShouldEqual(messageCount);

            bus.Stop();
        }

        [Test]
        public void SendTimeoutRequestAndWaitForTimeout()
        {
            var handler = new TimeoutCommandHandler();

            var bus = CreateBusFactory()
                .WithHandlers(typeof(TimeoutCommandHandler))
                .ConfigureContainer(x => x.ForSingletonOf<TimeoutCommandHandler>().Use(handler))
                .CreateAndStartBus();

            bus.Subscribe(Subscription.Matching<TimeoutCommand>(x => x.ServiceName == bus.PeerId.ToString()));

            var state = new State { Value = 42 };
            var requestTimeoutCommand = Timeout.BuildRequest("Testing", DateTime.Now.AddSeconds(15), state, bus.PeerId.ToString());
            bus.Send(requestTimeoutCommand);

            var stopwatch = Stopwatch.StartNew();

            if (handler.Signal.WaitOne(100.Seconds()))
            {
                Console.WriteLine("Yay for the orange game!");
                Console.WriteLine("Elapsed: {0}", stopwatch.Elapsed);
            }

            bus.Stop();
        }

        [Test]
        public void StartBusAndGetRidOfItLikeABadBoy()
        {
            CreateBusFactory().CreateAndStartBus();
        }

        [Test]
        public void StartBusAndStopItLikeABoyScout()
        {
            var bus = CreateBusFactory().CreateAndStartBus();

            Thread.Sleep(1000);

            bus.Stop();
        }

        [Test]
        public void PublishManualEvent()
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

        private static BusFactory CreateBusFactory()
        {
            return new BusFactory()
                .WithConfiguration("tcp://localhost:129", "Dev");
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
        public class State
        {
            [ProtoMember(1, IsRequired = true)]
            public int Value;
        }

        public class TimeoutCommandHandler : IMessageHandler<TimeoutCommand>
        {
            public readonly EventWaitHandle Signal = new AutoResetEvent(false);

            public void Handle(TimeoutCommand message)
            {
                var state = message.Deserialize<State>();
                Console.WriteLine("Timeout! Key: {0}, State: {1}", message.Key, state.Value);

                Signal.Set();
            }
        }

        public class AccumulatingTimeoutCommandHandler : IMessageHandler<TimeoutCommand>
        {
            public readonly List<TimeoutCommand> Commands = new List<TimeoutCommand>();

            public void Handle(TimeoutCommand message)
            {
                var state = message.Deserialize<State>();
                Console.WriteLine("Timeout! Key: {0}, State: {1}", message.Key, state.Value);
                Commands.Add(message);
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
    }
}
