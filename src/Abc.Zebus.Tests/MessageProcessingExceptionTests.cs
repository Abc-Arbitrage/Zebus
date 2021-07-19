using Abc.Zebus.Testing.Extensions;
using NUnit.Framework;

namespace Abc.Zebus.Tests
{
    [TestFixture]
    public class MessageProcessingExceptionTests
    {
        [Test]
        public void should_have_an_unknown_error_code_by_default()
        {
            var ex = new MessageProcessingException();
            ex.ErrorCode.ShouldEqual(1);
        }

        [Test]
        public void should_not_accept_a_success_error_code()
        {
            var ex = new MessageProcessingException { ErrorCode = 0 };
            ex.ErrorCode.ShouldEqual(1);
        }

        [Test]
        public void should_accept_custom_error_codes()
        {
            var ex = new MessageProcessingException { ErrorCode = 2 };
            ex.ErrorCode.ShouldEqual(2);
        }
    }
}
