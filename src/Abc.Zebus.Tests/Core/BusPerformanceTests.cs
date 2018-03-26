using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Core;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Scan;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Measurements;
using Abc.Zebus.Util;
using NUnit.Framework;
using ProtoBuf;

namespace Abc.Zebus.Tests.Core
{
    [TestFixture]
    [Ignore("ManualOnly")]
    [Category("ManualOnly")]
    public class BusPerformanceTests
    {
        // this must be a valid directory endpoint
        private static readonly string _directoryEndPoint = Environment.GetEnvironmentVariable("ZEBUS_TEST_DIRECTORY");

        [Test]
        public void MeasureCommandThroughputWithoutPersistence()
        {
            // 09/07/2013 - PC LAJ: 34k/s
            // 02/10/2013 - PC VAN: 27k/s
            const int messageCount = 300000;

            using (CreateAndStartReceiver())
            using (var sender = CreateAndStartSender())
            {
                using (Measure.Throughput(messageCount))
                {
                    Task task = null;
                    for (var i = 1; i <= messageCount; ++i)
                    {
                        task = sender.Send(new PerfCommand(i));
                    }
                    task.Wait();
                }

                Console.WriteLine(PerfHandler.LastValue);
            }
        }

        [Test]
        public void MeasureEventThroughputWithoutPersistence()
        {
            // 09/07/2013 LAJ: 80k/s
            // 02/10/2013 VAN: 60k/s
            // 25/11/2013 CAO: 66k/s
            // 10/12/2013 CAO: 72k/s

            const int messageCount = 1000 * 1000;

            using (CreateAndStartReceiver())
            using (var sender = CreateAndStartSender())
            {
                using (Measure.Throughput(messageCount))
                {
                    for (var i = 1; i <= messageCount; ++i)
                    {
                        sender.Publish(new PerfEvent(i));
                        Thread.SpinWait(1 << 4);
                    }

                    var spinWait = new SpinWait();
                    while (PerfHandler.LastValue != messageCount)
                        spinWait.SpinOnce();
                }

                Console.WriteLine(PerfHandler.LastValue);
            }
        }

        [Test]
        public void MeasureEventThroughputWithPersistence()
        {
            const int messageCount = 1000 * 1000;

            using (CreateAndStartReceiver(true))
            using (var sender = CreateAndStartSender())
            {
                using (Measure.Throughput(messageCount))
                {
                    for (var i = 1; i <= messageCount; ++i)
                    {
                        sender.Publish(new PersistentPerfEvent(i));
                        Thread.SpinWait(1 << 4);
                    }

                    var spinWait = new SpinWait();
                    while (PerfHandler.LastValue != messageCount)
                        spinWait.SpinOnce();
                }

                Console.WriteLine(PerfHandler.LastValue);
            }
        }

        [TestCase(100000, 10)]
        public void MeasureEventThroughputWithManyReceivers(int messageCount, int receiverCount)
        {
            // 10/12/2013 CAO: 8754/s

            var receivers = Enumerable.Repeat(0, receiverCount).Select(_ => CreateAndStartReceiver()).ToList();
            using (var sender = CreateAndStartSender())
            {
                Console.WriteLine("MessageCount: {0}, ReceiverCount: {1}", messageCount, receiverCount);
                try
                {
                    using (Measure.Throughput(messageCount))
                    {
                        for (var i = 1; i <= messageCount; ++i)
                        {
                            sender.Publish(new PerfEvent(i));
                        }

                        var spinWait = new SpinWait();
                        while (PerfHandler.CallCount != messageCount * receiverCount)
                            spinWait.SpinOnce();
                    }

                    Console.WriteLine(PerfHandler.LastValue);
                }
                finally
                {
                    receivers.ForEach(x =>
                    {
                        x.Stop();
                        Console.WriteLine("Receiver stopped " + x.PeerId);
                    });
                }
            }
            Console.WriteLine("Sender stopped");
        }

        [Test]
        public void MeasureLocalDispatch()
        {
            // 25/11/2013 - PC CAO: 190k/s
            // 23/09/2014 - PC CAO: 275k/s

            var bus = new BusFactory()
                .WithHandlers(typeof(PerfHandler))
                .CreateAndStartInMemoryBus();

            using (bus)
            using (DispatchQueue.SetCurrentDispatchQueueName(DispatchQueueNameScanner.DefaultQueueName))
            using (MessageContext.SetCurrent(MessageContext.CreateTest()))
            {
                Measure.Execution(500000, () => bus.Send(new PerfCommand(42)));
            }
        }

        public static IBus CreateAndStartSender()
        {
            return new BusFactory()
                .WithPeerId("Abc.Zebus.Perf.Sender.*")
                .WithConfiguration(new BusConfiguration(false, _directoryEndPoint), "Dev")
                .CreateAndStartBus();
        }

        public static IBus CreateAndStartReceiver(bool isPersistent = false)
        {
            return new BusFactory()
                .WithPeerId("Abc.Zebus.Perf.Receiver.*")
                .WithConfiguration(new BusConfiguration(isPersistent, _directoryEndPoint), "Dev")
                .WithHandlers(typeof(PerfHandler))
                .CreateAndStartBus();
        }

        private class BusConfiguration : IBusConfiguration
        {
            public BusConfiguration(bool isPersistent, string directoryServiceEndPoint)
            {
                DirectoryServiceEndPoints = new[] { directoryServiceEndPoint };
                RegistrationTimeout = 10.Second();
                IsPersistent = isPersistent;
            }

            public string[] DirectoryServiceEndPoints { get; }
            public TimeSpan RegistrationTimeout { get; }
            public TimeSpan StartReplayTimeout => 30.Seconds();
            public bool IsPersistent { get; }
            public bool IsDirectoryPickedRandomly => false;
            public bool IsErrorPublicationEnabled => false;
            public int MessagesBatchSize => 200;
        }

        [ProtoContract]
        public class PerfCommand : ICommand
        {
            [ProtoMember(1, IsRequired = true)]
            public readonly int Value;

            public PerfCommand(int value)
            {
                Value = value;
            }
        }

        [ProtoContract, Transient]
        public class PerfEvent : IEvent
        {
            [ProtoMember(1, IsRequired = true)]
            public readonly int Value;

            public PerfEvent(int value)
            {
                Value = value;
            }
        }

        [ProtoContract]
        public class PersistentPerfEvent : IEvent
        {
            [ProtoMember(1, IsRequired = true)]
            public readonly int Value;

            public PersistentPerfEvent(int value)
            {
                Value = value;
            }
        }

        public class PerfHandler : IMessageHandler<PerfCommand>, IMessageHandler<PerfEvent>, IMessageHandler<PersistentPerfEvent>
        {
            public static int LastValue;
            public static int CallCount;

            public void Handle(PerfCommand message)
            {
                LastValue = message.Value;
                Interlocked.Increment(ref CallCount);
            }

            public void Handle(PerfEvent message)
            {
                LastValue = message.Value;
                Interlocked.Increment(ref CallCount);
            }

            public void Handle(PersistentPerfEvent message)
            {
                LastValue = message.Value;
                Interlocked.Increment(ref CallCount);
            }
        }
    }
}
