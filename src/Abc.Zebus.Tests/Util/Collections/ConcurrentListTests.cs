using System;
using System.Diagnostics;
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
    public class ConcurrentListTests
    {
        [Test, Ignore]
        public void add_performance_test()
        {
            var list = new ConcurrentList<int>();
            var stopwatch = Stopwatch.StartNew();
            for (var i = 0; i < 10000; i++)
            {
                list.Add(i);
            }
            stopwatch.Stop();
            Console.WriteLine(stopwatch.Elapsed);
        }

        [Test, Ignore]
        public void modify_performance_test()
        {
            var list = new ConcurrentList<int>();
            for (var i = 0; i < 10000; i++)
            {
                list.Add(i);
            }

            var stopwatch = Stopwatch.StartNew();
            for (var i = 0; i < 100000; i++)
            {
                list[list.Count - 1] = 12;
            }
            stopwatch.Stop();
            Console.WriteLine(stopwatch.Elapsed);
        }

        [Test, Ignore]
        public void removed_at_performance_test()
        {
            var list = new ConcurrentList<int>();
            for (var i = 0; i < 100000; i++)
            {
                list.Add(i);
            }

            var stopwatch = Stopwatch.StartNew();
            for (var i = 0; i < 100000; i++)
            {
                list.RemoveAt(0);
            }
            stopwatch.Stop();
            Console.WriteLine(stopwatch.Elapsed);
        }

        [Test, Ignore]
        public void should_add_and_remove_concurrently()
        {
            var list = new ConcurrentList<int>();

            var t1 = Task.Run(() => Enumerable.Range(0, 100000).Select(x => 42).ForEach(list.Add));

            Thread.Sleep(100); // weaaaaak

            var t2 = Task.Run(() => Enumerable.Range(0, 100000).Select(x => 42).ForEach(x => list.Remove(x)));

            Task.WaitAll(t1, t2);

            list.Count.ShouldEqual(0);
        }

        [Test]
        public void should_add_concurrently()
        {
            var list = new ConcurrentList<int>();

            var t1 = Task.Run(() => Enumerable.Range(0, 1000).Select(x => 2 * x).ForEach(list.Add));
            var t2 = Task.Run(() => Enumerable.Range(0, 1000).Select(x => 2 * x + 1).ForEach(list.Add));

            Task.WaitAll(t1, t2);

            list.Count.ShouldEqual(2000);
        }

        [Test]
        public void should_add_items()
        {
            // Arrange
            var list = new ConcurrentList<int>();

            // Act
            list.Add(1);
            list.Add(2);
            list.Add(3);

            // assert
            list.Count.ShouldEqual(3);
            list[0].ShouldEqual(1);
            list[1].ShouldEqual(2);
            list[2].ShouldEqual(3);
        }

        [Test]
        public void should_clear()
        {
            // Arrange
            var list = new ConcurrentList<int> { 1, 2, 3 };

            // Act
            list.Clear();

            // assert
            list.Count.ShouldEqual(0);
        }

        [Test]
        public void should_edit_index()
        {
            // Arrange
            var list = new ConcurrentList<int> { 1, 2, 3 };

            // Act
            list[1] = 42;

            // assert
            list.Count.ShouldEqual(3);
            list[0].ShouldEqual(1);
            list[1].ShouldEqual(42);
            list[2].ShouldEqual(3);
        }

        [Test]
        public void should_insert_at_index()
        {
            // Arrange
            var list = new ConcurrentList<int> { 1, 2, 3 };

            // Act
            list.Insert(1, 42);

            // assert
            list.Count.ShouldEqual(4);
            list[0].ShouldEqual(1);
            list[1].ShouldEqual(42);
            list[2].ShouldEqual(2);
            list[3].ShouldEqual(3);
        }

        [Test]
        public void should_not_remove_non_existing_items()
        {
            // Arrange
            var list = new ConcurrentList<int> { 1, 2, 3 };

            // Act
            var removed = list.Remove(42);

            // assert
            removed.ShouldBeFalse();
            list.Count.ShouldEqual(3);
            list[0].ShouldEqual(1);
            list[1].ShouldEqual(2);
            list[2].ShouldEqual(3);
        }

        [Test]
        public void should_remove_at_index()
        {
            // Arrange
            var list = new ConcurrentList<int> { 1, 2, 3 };

            // Act
            list.RemoveAt(1);

            // assert
            list.Count.ShouldEqual(2);
            list[0].ShouldEqual(1);
            list[1].ShouldEqual(3);
        }

        [Test]
        public void should_remove_first_occurence_of_items()
        {
            // Arrange
            var list = new ConcurrentList<int> { 1, 2, 2, 2, 3 };

            // Act
            var removed = list.Remove(2);

            // assert
            removed.ShouldBeTrue();
            list.Count.ShouldEqual(4);
            list[0].ShouldEqual(1);
            list[1].ShouldEqual(2);
            list[2].ShouldEqual(2);
            list[3].ShouldEqual(3);
        }

        [Test]
        public void should_remove_items()
        {
            // Arrange
            var list = new ConcurrentList<int> { 1, 2, 3 };

            // Act
            var removed = list.Remove(2);

            // assert
            removed.ShouldBeTrue();
            list.Count.ShouldEqual(2);
            list[0].ShouldEqual(1);
            list[1].ShouldEqual(3);
        }
    }
}