using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
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
    [TestFixture]
    [Ignore]
    [Category("ManualOnly")]
    public class BusManualTests
    {
        // this must be a valid directory endpoint
        private const string _directoryEndPoint = "tcp://localhost:129";

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

        private static BusFactory CreateBusFactory()
        {
            return new BusFactory().WithConfiguration(_directoryEndPoint, "Dev");
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
    }
}