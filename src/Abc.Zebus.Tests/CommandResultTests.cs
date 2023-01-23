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

        [TestCase(0, null, "Success")]
        [TestCase(1, null, "Error, ErrorCode: 1")]
        [TestCase(256, null, "Error, ErrorCode: 256")]
        [TestCase(256, "Expected message", "Error, ErrorCode: 256, ResponseMessage: [Expected message]")]
        public void should_get_string_from_result(int errorCode, string responseMessage, string expectedText)
        {
            var commandResult = new CommandResult(errorCode, responseMessage, null);

            commandResult.ToString().ShouldEqual(expectedText);
        }

        [TestCase(0, null, "Success, Response: [Response!]")]
        [TestCase(256, null, "Error, ErrorCode: 256, Response: [Response!]")]
        [TestCase(256, "Expected message", "Error, ErrorCode: 256, ResponseMessage: [Expected message], Response: [Response!]")]
        public void should_get_string_from_result_with_response(int errorCode, string responseMessage, string expectedText)
        {
            var commandResult = new CommandResult(errorCode, responseMessage, new Response());

            commandResult.ToString().ShouldEqual(expectedText);
        }

        private class Response
        {
            public override string ToString()
            {
                return $"Response!";
            }
        }
    }
}
