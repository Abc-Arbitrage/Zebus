using System;
using System.Linq;
using Abc.Zebus.Testing.Extensions;
using NUnit.Framework;
using Buffer = Abc.Zebus.Util.Buffer;

namespace Abc.Zebus.Tests.Util.GenZero
{
    [TestFixture]
    public class BufferTests
    {
        [Test]
        public void should_copy_from_buffer()
        {
            var src = new Buffer(new byte[] { 0, 1, 2, 3, 4 });
            var dest = new Buffer(10);

            dest.CopyFrom(ref src);

            dest.Length.ShouldEqual(src.Length);
            dest.Data.Take(dest.Length).ShouldEqual(src.Data.Take(src.Length));
        }

        [Test]
        public void should_copy_from_byte_array()
        {
            var buffer = new Buffer(10);
            var bytes = new byte[] { 0, 1, 2, 3, 4 };

            buffer.CopyFrom(bytes);

            buffer.Length.ShouldEqual(bytes.Length);
            buffer.Data.Take(buffer.Length).ShouldEqual(bytes);
        }

        [Test]
        public void should_copy_to_buffer()
        {
            var src = new Buffer(new byte[] { 0, 1, 2, 3, 4 });
            var dest = new Buffer(10);

            src.CopyTo(ref dest);

            dest.Length.ShouldEqual(src.Length);
            dest.Data.Take(dest.Length).ShouldEqual(src.Data.Take(src.Length));
        }

        [Test]
        public void should_throw_when_assigned_length_is_greater_than_the_available_byte_count()
        {
            var buffer = new Buffer(new byte[] { 0, 1, 2, 3, 4 });

            Assert.DoesNotThrow(() => buffer.Length = 2);
            Assert.DoesNotThrow(() => buffer.Length = 5);
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Length = 10);
        }

        [Test]
        public void should_throw_when_assigned_negative_length()
        {
            var buffer = new Buffer(new byte[] { 0, 1, 2, 3, 4 });

            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Length = -1);
        }

        [Test]
        public void should_only_take_length_bytes_into_account_for_hashcode()
        {
            var b1 = new Buffer(new byte[] { 0, 1, 2, 3, 4 });
            var b2 = new Buffer(new byte[] { 0, 1 });

            b1.Length = 2;

            b1.GetHashCode().ShouldEqual(b2.GetHashCode());
        }

        [Test]
        public void should_throw_when_destination_buffer_is_too_small()
        {
            var src = new Buffer(new byte[] { 0, 1, 2, 3, 4 });
            var dest = new Buffer(2);

            Assert.Throws<ArgumentException>(() => src.CopyTo(ref dest));
        }

        [Test]
        public void should_to_byte_array_return_correct_bytes()
        {
            var byteArray = new byte[] { 0, 1, 2, 3, 4 };
            var buffer = new Buffer(byteArray);

            var otherByteArray = buffer.ToByteArray();

            otherByteArray.ShouldEqual(byteArray);
        }

        [Test]
        public void should_two_equal_buffers_have_the_same_hashcode()
        {
            var b1 = new Buffer(new byte[] { 0, 1, 2, 3, 4 });
            var b2 = new Buffer(new byte[] { 0, 1, 2, 3, 4 });

            b1.GetHashCode().ShouldEqual(b2.GetHashCode());
        }


        [Test]
        public void should_copy_from_byte_array_with_offset_and_length()
        {
            var buffer = new Buffer(10);
            var bytes = new byte[] { 0, 1, 2, 3, 4 };

            buffer.CopyFrom(bytes, 2, 1);

            buffer.Length.ShouldEqual(1);
            buffer.Data.Take(buffer.Length).ShouldEqual(new byte[] { 2 });
        }
    }
}