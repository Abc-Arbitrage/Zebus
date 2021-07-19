using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Core;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Testing;
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
        private const string _directoryEndPoint = "tcp://localhost:129";

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

        [Test]
        public void should_generate_unacked_messages()
        {
            var targetConfig = new BusConfiguration(_directoryEndPoint)
            {
                IsPersistent = true,
            };

            var target = CreateBusFactory().WithHandlers(typeof(ManualEventHandler))
                                           .WithConfiguration(targetConfig, "Demo")
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
            return new BusFactory().WithConfiguration(_directoryEndPoint, "Demo");
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
    }
}
