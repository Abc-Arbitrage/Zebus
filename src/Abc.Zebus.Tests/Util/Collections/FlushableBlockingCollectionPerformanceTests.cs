using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Util.Collections;
using Abc.Zebus.Util.Extensions;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Util.Collections
{
    [TestFixture]
    [Ignore]
    [Category("ManualOnly")]
    public class FlushableBlockingCollectionPerformanceTests
    {
        [Test]
        public void MeasureEveryThing()
        {
            Console.Write("FBC: ");
            MeasureThroughput();

            Console.Write("BC : ");
            MeasureThroughputRef();

            Console.Write("FBC latency (ms): ");
            MeasureLatency();

            Console.Write("BC  latency (ms): ");
            MeasureLatencyRef();
        }

        [Test]
        public void MeasureThroughput()
        {
            var queue = new FlushableBlockingCollection<int>();

            var watch = Stopwatch.StartNew();

            var enqueue = Task.Run(() => Enumerable.Range(0, 50000000).ForEach(queue.Add));
            var dequeue = Task.Run(() => queue.GetConsumingEnumerable().ForEach(x => { }));

            enqueue.Wait();
            queue.CompleteAdding();

            dequeue.Wait();

            Console.WriteLine("{0} items processed in {1}", 50000000, watch.Elapsed);
        }

        [Test]
        public void MeasureThroughputRef()
        {
            var queue = new BlockingCollection<int>();

            var watch = Stopwatch.StartNew();

            var enqueue = Task.Run(() => Enumerable.Range(0, 50000000).ForEach(queue.Add));
            var dequeue = Task.Run(() => queue.GetConsumingEnumerable().ForEach(x => { }));

            enqueue.Wait();
            queue.CompleteAdding();

            dequeue.Wait();

            Console.WriteLine("{0} items processed in {1}", 50000000, watch.Elapsed);
        }

        [Test]
        public void MeasureLatency()
        {
            var queue = new FlushableBlockingCollection<Stopwatch>();

            var elapsed = TimeSpan.Zero;
            var dequeue = Task.Run(() => queue.GetConsumingEnumerable().ForEach(x => elapsed += x.Elapsed));

            for (var i = 0; i < 5000; ++i)
            {
                if (i % 30 == 0)
                    Thread.Sleep(100);

                queue.Add(Stopwatch.StartNew());
            }

            queue.CompleteAdding();

            dequeue.Wait();

            Console.WriteLine("{0}", elapsed.TotalMilliseconds / 5000);
        }

        [Test]
        public void MeasureLatencyRef()
        {
            var queue = new BlockingCollection<Stopwatch>();

            var elapsed = TimeSpan.Zero;
            var dequeue = Task.Run(() => queue.GetConsumingEnumerable().ForEach(x => elapsed += x.Elapsed));

            for (var i = 0; i < 5000; ++i)
            {
                if (i % 30 == 0)
                    Thread.Sleep(100);

                queue.Add(Stopwatch.StartNew());
            }

            queue.CompleteAdding();

            dequeue.Wait();

            Console.WriteLine("{0}", elapsed.TotalMilliseconds / 5000);
        }

        [Test]
        public void MeasureCpuUsage()
        {
            var queue = new FlushableBlockingCollection<Stopwatch>();

            var dequeue = Task.Run(() => queue.GetConsumingEnumerable().ForEach(x => { }));

            Console.WriteLine("Use the Process Explorer, Luke");

            Thread.Sleep(20000);

            queue.CompleteAdding();

            dequeue.Wait();
        }

        [Test]
        public void MeasureCpuUsageRef()
        {
            var queue = new BlockingCollection<Stopwatch>();

            var dequeue = Task.Run(() => queue.GetConsumingEnumerable().ForEach(x => { }));

            Console.WriteLine("Use the Process Explorer, Luke");

            Thread.Sleep(20000);

            queue.CompleteAdding();

            dequeue.Wait();
        }
    }
}