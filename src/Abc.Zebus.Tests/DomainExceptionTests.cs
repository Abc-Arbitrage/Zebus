using Abc.Zebus.Testing.Extensions;
using NUnit.Framework;

namespace Abc.Zebus.Tests
{
    [TestFixture]
    public class DomainExceptionTests
    {
        public static class FakeErrorCode
        {
            [System.ComponentModel.Description("This is a fake error message")]
            public static int SomeErrorValue
            {
                get { return 1000 + 42; }
            }

            [System.ComponentModel.Description("This is a fake error message with a formatted parameter {0}")]
            public static int AnotherErrorValue
            {
                get { return 1000 + 42; }
            }

            public static int AnotherAnotherErrorValue
            {
                get { return 1000 + 42; }
            }

            public const int ContantErrorValue = 1000 + 42;
        }

        [Test]
        public void should_obtain_error_message_via_attribute()
        {
            var ex = new DomainException(() => FakeErrorCode.SomeErrorValue);

            ex.ErrorCode.ShouldEqual(FakeErrorCode.SomeErrorValue);
            ex.Message.ShouldEqual("This is a fake error message");
        }

        [Test]
        public void should_obtain_error_message_via_attribute_with_formatted_parameter()
        {
            var ex = new DomainException(() => FakeErrorCode.AnotherErrorValue, "formatted param");

            ex.ErrorCode.ShouldEqual(FakeErrorCode.AnotherErrorValue);
            ex.Message.ShouldEqual("This is a fake error message with a formatted parameter formatted param");
        }

        [Test]
        public void should_not_fail_if_attribute_is_not_defined()
        {
            var ex = new DomainException(() => FakeErrorCode.AnotherAnotherErrorValue);

            ex.ErrorCode.ShouldEqual(FakeErrorCode.AnotherAnotherErrorValue);
            ex.Message.ShouldEqual(string.Empty);
        }

        [Test]
        public void should_not_fail_if_expression_returns_constant()
        {
            var ex = new DomainException(() => FakeErrorCode.ContantErrorValue );

            ex.ErrorCode.ShouldEqual(FakeErrorCode.ContantErrorValue);
            ex.Message.ShouldEqual(string.Empty);
        }

        [Test]
        public void should_not_fail_if_expression_returns_direct_value()
        {
            var ex = new DomainException(() => 3712 );

            ex.ErrorCode.ShouldEqual(3712);
            ex.Message.ShouldEqual(string.Empty);
        }
    }
}
