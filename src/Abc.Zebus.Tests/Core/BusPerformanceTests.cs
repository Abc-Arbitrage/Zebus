using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Core;
using Abc.Zebus.Scan;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Measurements;
using NUnit.Framework;
using ProtoBuf;

namespace Abc.Zebus.Tests.Core
{
    [TestFixture]
    [Ignore]
    [Category("ManualOnly")]
    public class BusPerformanceTests
    {
        // this must be a valid directory endpoint
        private const string _directoryEndPoint = "tcp://<directory-address>:<port>";

        [Test]
        public void MeasureCommandThroughputWithoutPersistence()
        {
            // 09/07/2013 - PC LAJ: 34k/s
            // 02/10/2013 - PC VAN: 27k/s
            const int messageCount = 300000;

            var receiver = CreateAndStartReceiver();
            var sender = CreateAndStartSender();

            using (Measure.Throughput(messageCount))
            {
                Task task = null;
                for (var i = 1; i <= messageCount; ++i)
                {
                    task = sender.Send(new PerfCommand(i));
                }
                task.Wait();
            }

            Console.WriteLine(PerfCommandHandler.LastValue);

            receiver.Stop();
            sender.Stop();
        }

        [Test]
        public void MeasureEventThroughputWithoutPersistence()
        {
            // 09/07/2013 LAJ: 80k/s
            // 02/10/2013 VAN: 60k/s
            // 25/11/2013 CAO: 66k/s
            // 10/12/2013 CAO: 72k/s

            const int messageCount = 500000;

            var receiver = CreateAndStartReceiver();
            var sender = CreateAndStartSender();

            using (Measure.Throughput(messageCount))
            {
                for (var i = 1; i <= messageCount; ++i)
                {
                    sender.Publish(new PerfEvent(i));
                }

                var spinWait = new SpinWait();
                while (PerfEventHandler.LastValue != messageCount)
                    spinWait.SpinOnce();
            }

            Console.WriteLine(PerfEventHandler.LastValue);

            receiver.Stop();
            sender.Stop();
        }

        [TestCase(100000, 10)]
        public void MeasureEventThroughputWithManyReceivers(int messageCount, int receiverCount)
        {
            // 10/12/2013 CAO: 8754/s

            var receivers = Enumerable.Repeat(0, receiverCount).Select(_ => CreateAndStartReceiver()).ToList();
            var sender = CreateAndStartSender();

            Console.WriteLine("MessageCount: {0}, ReceiverCount: {1}", messageCount, receiverCount);

            using (Measure.Throughput(messageCount))
            {
                for (var i = 1; i <= messageCount; ++i)
                {
                    sender.Publish(new PerfEvent(i));
                }

                var spinWait = new SpinWait();
                while (PerfEventHandler.CallCount != messageCount * receiverCount)
                    spinWait.SpinOnce();
            }

            Console.WriteLine(PerfEventHandler.LastValue);

            receivers.ForEach(x =>
            {
                x.Stop();
                Console.WriteLine("Receiver stopped " + x.PeerId);
            });

            sender.Stop();
            Console.WriteLine("Sender stopped");
        }

        [Test]
        public void MeasureLocalDispatch()
        {
            // 25/11/2013 - PC CAO: 190k/s
            // 23/09/2014 - PC CAO: 275k/s

            var bus = new BusFactory()
                .WithHandlers(typeof(PerfCommandHandler), typeof(PerfEventHandler))
                .CreateAndStartInMemoryBus();

            using (MessageContext.SetCurrent(MessageContext.CreateTest().WithDispatchQueueName(DispatchQueueNameScanner.DefaultQueueName)))
            {
                Measure.Execution(500000, () => bus.Send(new PerfCommand(42)));
            }

            bus.Stop();
        }

        public static IBus CreateAndStartSender()
        {
            return new BusFactory()
                .WithPeerId("Abc.Zebus.Perf.Sender.*")
                .WithConfiguration(_directoryEndPoint, "Dev")
                .CreateAndStartBus();
        }

        public static IBus CreateAndStartReceiver()
        {
            return new BusFactory()
                .WithPeerId("Abc.Zebus.Perf.Receiver.*")
                .WithConfiguration(_directoryEndPoint, "Dev")
                .WithHandlers(typeof(PerfCommandHandler), typeof(PerfEventHandler))
                .CreateAndStartBus();
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

        public class PerfCommandHandler : IMessageHandler<PerfCommand>
        {
            public static int LastValue;

            public void Handle(PerfCommand message)
            {
                LastValue = message.Value;
            }
        }

        public class PerfEventHandler : IMessageHandler<PerfEvent>
        {
            public static int LastValue;
            public static int CallCount;

            public void Handle(PerfEvent message)
            {
                LastValue = message.Value;

                Interlocked.Increment(ref CallCount);
            }
        }
    }
}