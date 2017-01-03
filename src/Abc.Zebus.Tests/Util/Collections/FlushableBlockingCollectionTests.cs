using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util.Collections;
using Abc.Zebus.Util.Extensions;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Util.Collections
{
    [TestFixture]
    public class FlushableBlockingCollectionTests
    {
        [TestCase(1)]
        [TestCase(20)]
        [TestCase(200)]
        public void should_get_consuming_enumerable(int batchSize)
        {
            var consumedItems = new List<int>();

            var bc = new FlushableBlockingCollection<int>();

            var t1 = Task.Run(() => Enumerable.Range(0, 1000000).Select(x => 2 * x).ForEach(bc.Add));
            var t2 = Task.Run(() => Enumerable.Range(0, 1000000).Select(x => 2 * x + 1).ForEach(bc.Add));
            var consume = Task.Run(() => bc.GetConsumingEnumerable(batchSize).ForEach(consumedItems.AddRange));

            Task.WaitAll(t1, t2);

            bc.CompleteAdding();

            consume.Wait();
            var expectedItems = Enumerable.Range(0, 2000000).ToHashSet();

            consumedItems.Count.ShouldEqual(expectedItems.Count);
            foreach (var item in consumedItems)
            {
                expectedItems.Contains(item).ShouldBeTrue();
            }
        }

        [Test]
        public void should_flush_collection_with_multiple_writers()
        {
            var collection = new FlushableBlockingCollection<int>();

            var consumedItems = new List<int>();
            var consume = Task.Run(() =>
            {
                foreach (var batch in collection.GetConsumingEnumerable(5))
                {
                    consumedItems.AddRange(batch);

                    // simulate consumption lag
                    Thread.Sleep(0);
                }

                Console.WriteLine("{0} consumed items", consumedItems.Count);
                Console.WriteLine("Consumer done");
            });

            const int writerItemCount = 30000;

            var t1 = Task.Run(() =>
            {
                foreach (var item in Enumerable.Range(0, writerItemCount).Select(x => 3 * x))
                {
                    collection.Add(item);
                    Thread.SpinWait(1 << 4);
                }
                Console.WriteLine("T1 done");
            });
            var t2 = Task.Run(() =>
            {
                foreach (var item in Enumerable.Range(0, writerItemCount).Select(x => 3 * x + 1))
                {
                    collection.Add(item);
                    Thread.SpinWait(1 << 4);
                }
                Console.WriteLine("T2 done");
            });
            var t3 = Task.Run(() =>
            {
                foreach (var item in Enumerable.Range(0, writerItemCount).Select(x => 3 * x + 2))
                {
                    collection.Add(item);
                    Thread.SpinWait(1 << 4);
                    Thread.SpinWait(1 << 4);
                }
                Console.WriteLine("T3 done");
            });

            Thread.Sleep(100);

            Console.WriteLine("Flush #1");
            var flushedItems1 = collection.Flush();

            Thread.Sleep(100);

            Console.WriteLine("Flush #2");
            var flushedItems2 = collection.Flush();

            Task.WaitAll(t1, t2, t3);

            collection.CompleteAdding();
            consume.Wait();

            Console.WriteLine("{0} flushed items (#1)", flushedItems1.Count);
            Console.WriteLine("{0} flushed items (#2)", flushedItems2.Count);

            var exectedItems = Enumerable.Range(0, writerItemCount * 3).ToHashSet();
            var items = consumedItems.Concat(flushedItems1).Concat(flushedItems2).ToList();
            items.Count.ShouldEqual(exectedItems.Count);
            foreach (var item in items)
            {
                exectedItems.Contains(item).ShouldBeTrue();
            }
        }

        [Test]
        public void should_flush_collection_with_single_writer()
        {
            var collection = new FlushableBlockingCollection<int>();

            var consumedItems = new List<int>();
            var consume = Task.Run(() =>
            {
                foreach (var batch in collection.GetConsumingEnumerable(10))
                {
                    consumedItems.AddRange(batch);

                    // simulate very slow consumer
                    Thread.Sleep(2);
                }

                Console.WriteLine("{0} consumed items", consumedItems.Count);
                Console.WriteLine("Consumer done");
            });

            const int batchSize = 500000;

            foreach (var item in Enumerable.Range(0 * batchSize, batchSize))
            {
                collection.Add(item);
            }

            Thread.Sleep(100);
            Console.WriteLine("Flush #1");
            var flushedItems1 = collection.Flush();
            Console.WriteLine("{0} flushed items", flushedItems1.Count);

            foreach (var item in Enumerable.Range(1 * batchSize, batchSize))
            {
                collection.Add(item);
            }

            Thread.Sleep(100);
            Console.WriteLine("Flush #2");
            var flushedItems2 = collection.Flush();
            Console.WriteLine("{0} flushed items", flushedItems2.Count);

            foreach (var item in Enumerable.Range(2 * batchSize, batchSize))
            {
                collection.Add(item);
            }

            Thread.Sleep(100);
            Console.WriteLine("Flush #3");
            var flushedItems3 = collection.Flush();
            Console.WriteLine("{0} flushed items", flushedItems3.Count);

            collection.CompleteAdding();
            consume.Wait();

            var exectedItems = Enumerable.Range(0, 1500000).ToHashSet();
            var items = consumedItems.Concat(flushedItems1).Concat(flushedItems2).Concat(flushedItems3).ToList();
            items.Count.ShouldEqual(exectedItems.Count);
            foreach (var item in items)
            {
                exectedItems.Contains(item).ShouldBeTrue();
            }
        }

        [Test]
        public void should_flush_multiple_times()
        {
            var collection = new FlushableBlockingCollection<int>();

            var consumedItems = new List<int>();
            var consumerTask = Task.Run(() =>
            {
                foreach (var batch in collection.GetConsumingEnumerable(10))
                {
                    consumedItems.AddRange(batch);
                    Thread.Yield();
                }
                Console.WriteLine("Consumer done");
            });

            var flushedItems = new List<ConcurrentQueue<int>>();
            var flusherTask = Task.Run(() =>
            {
                for (var i = 0; i < 10; ++i)
                {
                    flushedItems.Add(collection.Flush());
                    Thread.Sleep(50);
                }
                Console.WriteLine("Flusher done");
            });

            var adderTask = Task.Run(() =>
            {
                foreach (var item in Enumerable.Range(0, 5000000).Select(x => 3 * x))
                {
                    collection.Add(item);
                    Thread.Yield();
                }
                Console.WriteLine("Adder done");
            });

            adderTask.Wait();

            collection.CompleteAdding();

            Task.WaitAll(consumerTask, flusherTask);

            var flushedItemCount = flushedItems.Sum(x => x.Count);
            Console.WriteLine("{0} flushed items", flushedItemCount);

            consumedItems.AddRange(flushedItems.SelectMany(x => x));
            consumedItems.Count.ShouldEqual(5000000);
        }
    }
}