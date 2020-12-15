using System;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Core;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Routing;
using Abc.Zebus.Scan;
using Abc.Zebus.Testing;
using Abc.Zebus.Tests.Core;
using Abc.Zebus.Util;
using ProtoBuf;

namespace Abc.Zebus.Tests
{
    public class Program
    {
        public static void MainLol(string[] args)
        {
            new Log4netConfigurator().Setup();

            if (args.FirstOrDefault() == "/receive")
            {
                RunReceiver();
                return;
            }

            if (args.FirstOrDefault() == "/send")
            {
                RunSender();
                return;
            }

            if (args.FirstOrDefault() == "/receive-routed")
            {
                RunRoutedReceiver();
                return;
            }

            if (args.FirstOrDefault() == "/send-routed")
            {
                SendRoutedMessage();
                return;
            }

            if (args.FirstOrDefault() == "/send-local")
            {
                RunLocalDispatch();
                return;
            }

            var test = new BusPerformanceTests();
            test.MeasureEventThroughputWithoutPersistence();

            Console.ReadKey();
        }

        private static void RunLocalDispatch()
        {
            var bus = new BusFactory()
                      .WithHandlers(typeof(BusPerformanceTests.PerfHandler))
                      .CreateAndStartInMemoryBus();

            Console.WriteLine("Press any key to start");
            Console.ReadKey();

            var running = true;

            var runTask = Task.Run(() =>
            {
                using (DispatchQueue.SetCurrentDispatchQueueName(DispatchQueueNameScanner.DefaultQueueName))
                using (MessageContext.SetCurrent(MessageContext.CreateTest()))
                {
                    while (running)
                    {
                        bus.Send(new BusPerformanceTests.PerfCommand(42));
                    }
                }
            });

            Console.WriteLine("Press any key to exit");
            Console.ReadKey();

            running = false;
            runTask.Wait();

            bus.Stop();
        }

        private static void RunSender()
        {
            var bus = BusPerformanceTests.CreateAndStartSender();

            var running = true;
            var task = Task.Run(() =>
            {
                var i = 0;
                while (running)
                {
                    //bus.Send(new BusPerformanceTests.PerfCommand(i));
                    bus.Publish(new BusPerformanceTests.PerfEvent(i));
                    ++i;
                }
            });

            Console.WriteLine("Sender started, press any key to exit...");
            Console.ReadKey();

            running = false;
            task.Wait();

            bus.Stop();
        }

        private static void RunReceiver()
        {
            var bus = BusPerformanceTests.CreateAndStartReceiver();

            Console.WriteLine("Receiver started, press any key to exit...");
            Console.ReadKey();

            bus.Stop();
        }

        private static void RunRoutedReceiver()
        {
            var bus = CreateAndStartReceiver();

            bus.Subscribe(Subscription.Matching<RoutableCommand>(x => x.RoutingKey == "Test"));

            Console.WriteLine("Receiver started, press any key to exit...");
            Console.ReadKey();

            bus.Stop();
        }

        private static void SendRoutedMessage()
        {
            var bus = CreateAndStartSender();

            var value = 42;
            while (true)
            {
                Console.WriteLine("Press s to send command, press any key to exit...");
                var key = Console.ReadKey();
                if (key.KeyChar != 's')
                    break;

                var task = bus.Send(new RoutableCommand("Test", value));
                Console.Write("Command sent, waiting for reply...");

                if (task.Wait(5.Seconds()))
                    Console.WriteLine(" reply received :)");
                else
                    Console.WriteLine(" timeout :(");

                ++value;
            }

            bus.Stop();
        }

        private static IBus CreateAndStartSender()
        {
            return new BusFactory()
                   .WithConfiguration("tcp://localhost:129", "Demo")
                   .WithPeerId("Abc.Zebus.Test.Sender.*")
                   .CreateAndStartBus();
        }

        private static IBus CreateAndStartReceiver()
        {
            return new BusFactory()
                   .WithConfiguration("tcp://localhost:129", "Demo")
                   .WithPeerId("Abc.Zebus.Test.Receiver.*")
                   .WithHandlers(typeof(RoutableCommandHandler))
                   .CreateAndStartBus();
        }

        [ProtoContract, Routable]
        public class RoutableCommand : ICommand
        {
            [ProtoMember(1, IsRequired = true), RoutingPosition(1)]
            public readonly string RoutingKey;

            [ProtoMember(2, IsRequired = true), RoutingPosition(2)]
            public readonly int Value;

            public RoutableCommand(string routingKey, int value)
            {
                RoutingKey = routingKey;
                Value = value;
            }

            public override string ToString() => $"RoutingKey: {RoutingKey}, Value: {Value}";
        }

        public class RoutableCommandHandler : IMessageHandler<RoutableCommand>
        {
            public void Handle(RoutableCommand message)
            {
            }
        }
    }
}
