using System.Linq;
using Abc.Zebus.Util;
using NUnit.Framework;

namespace Abc.Zebus.Testing.Extensions
{
    internal static class ExtendBuffer
    {
        public static void ShouldEqual(this Buffer actual, ref Buffer expected, string message = null)
        {
            Assert.AreEqual(expected.Length, actual.Length, message);
            Assert.AreEqual(expected.Data.Take(expected.Length), actual.Data.Take(actual.Length), message);
        }
    }
}