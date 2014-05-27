using System.Linq;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util.Collections;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Util.Collections
{
    [TestFixture]
    public class ConcurrentSetTests
    {
        [Test]
        public void should_add_an_item()
        {
            var setToRemoveFrom = new ConcurrentSet<int>(Enumerable.Range(1, 5));

            setToRemoveFrom.Add(6);

            setToRemoveFrom.ShouldEqual(new[] { 1, 2, 3, 4, 5, 6 });
        }

        [Test]
        public void should_remove_an_item()
        {
            var setToRemoveFrom = new ConcurrentSet<int>(Enumerable.Range(1, 5));

            setToRemoveFrom.Remove(3);

            setToRemoveFrom.ShouldEqual(new[] { 1, 2, 4, 5 });
        }

        [Test]
        public void should_test_that_an_item_is_contained()
        {
            var setToRemoveFrom = new ConcurrentSet<int>(Enumerable.Range(1, 5));

            setToRemoveFrom.Contains(3).ShouldBeTrue();
            setToRemoveFrom.Contains(7).ShouldBeFalse();
        }

        [Test]
        public void should_copy_itself_to_an_array()
        {
            var setToCopy = new ConcurrentSet<int>(Enumerable.Range(1, 5));
            var destinationArray = new int[5];

            setToCopy.CopyTo(destinationArray, 0);

            destinationArray.ShouldEqual(new[] { 1, 2, 3, 4, 5 });
        }

        [Test]
        public void should_clear_itself()
        {
            var setToClear = new ConcurrentSet<int>(Enumerable.Range(1, 5));

            setToClear.Clear();

            setToClear.Count.ShouldEqual(0);
        }

        [Test]
        public void should_be_readonly()
        {
            new ConcurrentSet<int>().IsReadOnly.ShouldBeFalse();
        }
    }
}