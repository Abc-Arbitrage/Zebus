#region (c)2009 Lokad - New BSD license

// Copyright (c) Lokad 2009 
// Company: http://www.lokad.com
// This code is released under the terms of the new BSD licence

#endregion

using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Util.Extensions;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Util.Extensions
{
    public class ExtendICollectionTests
    {
        [Test]
        public void Test_AddRange()
        {
            ICollection<int> collection = new[] { 1, 2, 3 }.ToList();
            var returned = collection.AddRange(new[] { 4, 5 });

            Assert.AreSame(collection, returned);
            CollectionAssert.AreEquivalent(collection, new[] { 1, 2, 3, 4, 5 });
        }

        [Test]
        public void Test_RemoveRange()
        {
            ICollection<int> collection = new[] { 1, 2, 3 }.ToList();
            var returned = collection.RemoveRange(new[] { 1, 4 });

            Assert.AreSame(collection, returned);
            CollectionAssert.AreEquivalent(collection, new[] { 2, 3 });
        }
    }
}