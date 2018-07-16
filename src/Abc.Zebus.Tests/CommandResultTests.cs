using Abc.Zebus.Testing.Extensions;
using NUnit.Framework;

namespace Abc.Zebus.Tests
{
    [TestFixture]
    public class CommandResultTests
    {
        private enum FakeEnumErrorCode
        {
            [System.ComponentModel.Description("This is a fake error message")]
            SomeErrorValue = 1,

            [System.ComponentModel.Description("This is a fake {0} error message")]
            SomeErrorValueWithFormat = 2,
            NoDescriptionErrorValue = 3
        }

        [Test]
        public void should_retrieve_empty_error_message_from_command_result_with_no_error()
        {
            var cmdResult = new CommandResult(0, null, null);

            cmdResult.GetErrorMessageFromEnum<FakeEnumErrorCode>().ShouldBeEmpty();
        }

        [Test]
        public void should_retrieve_error_message_from_command_result_with_enum_description()
        {
            var cmdResult = new CommandResult((int)FakeEnumErrorCode.SomeErrorValue, null, null);

            cmdResult.GetErrorMessageFromEnum<FakeEnumErrorCode>().ShouldEqual("This is a fake error message");
        }

        [Test]
        public void should_retrieve_error_message_from_command_result_with_enum_description_and_format()
        {
            var cmdResult = new CommandResult((int)FakeEnumErrorCode.SomeErrorValueWithFormat, null, null);

            cmdResult.GetErrorMessageFromEnum<FakeEnumErrorCode>("formated").ShouldEqual("This is a fake formated error message");
        }

        [Test]
        public void should_retrieve_empty_error_message_from_command_result_with_enum_no_description()
        {
            var cmdResult = new CommandResult((int)FakeEnumErrorCode.NoDescriptionErrorValue, null, null);

            cmdResult.GetErrorMessageFromEnum<FakeEnumErrorCode>().ShouldBeEmpty();
        }
    }
}
