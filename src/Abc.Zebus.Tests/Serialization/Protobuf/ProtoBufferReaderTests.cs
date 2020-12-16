using System;
using System.Linq;
using Abc.Zebus.Serialization.Protobuf;
using Abc.Zebus.Testing.Extensions;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Serialization.Protobuf
{
    [TestFixture]
    public class ProtoBufferReaderTests
    {
        [TestCase(10, "...")]
        [TestCase(16, "")]
        [TestCase(50, "")]
        public void should_create_debug_string(int limit, string prefix)
        {
            // Arrange
            var bytes = Guid.NewGuid().ToByteArray();
            var reader = new ProtoBufferReader(bytes, 16);

            // Act
            var debugString = reader.ToDebugString(limit);

            // Assert
            var expectedString = Convert.ToBase64String(bytes.Take(limit).ToArray()) + prefix;
            debugString.ShouldEqual(expectedString);
        }
    }
}
