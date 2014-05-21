using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using ABC.ServiceBus.Contracts;
using Abc.Zebus.Core;
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

        [Test]
        public void SendSeveralTimeoutRequestAndCheckForResponseUnicity()
        {
            var handler = new AccumulatingTimeoutCommandHandler();

            var bus = new BusFactory() 
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

            var bus = new BusFactory()
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
            new BusFactory().CreateAndStartBus();
        }

        [Test]
        public void StartBusAndStopItLikeABoyScout()
        {
            var bus = new BusFactory().CreateAndStartBus();

            Thread.Sleep(1000);

            bus.Stop();
        }
    }
}
