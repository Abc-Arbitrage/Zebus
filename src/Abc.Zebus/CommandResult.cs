using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Abc.Zebus.Util.Extensions;

namespace Abc.Zebus
{
    /// <summary>
    /// Contains the result of a bus command.
    /// </summary>
    /// <remarks>
    /// <see cref="CommandResult"/> should probably not be instantiated by user code outside unit tests.
    /// </remarks>
    public class CommandResult
    {
        [Obsolete("Use the constructor with the responseMessage parameter")]
        public CommandResult(int errorCode, object? response)
            : this(errorCode, null, response)
        {
        }

        public CommandResult(int errorCode, string? responseMessage, object? response)
        {
            ErrorCode = errorCode;
            ResponseMessage = responseMessage;
            Response = response;
        }

        public int ErrorCode { get; }
        public string? ResponseMessage { get; }
        public object? Response { get; }

        public bool IsSuccess => ErrorCode == 0;

        public string GetErrorMessageFromEnum<T>(params object[] formatValues) where T : struct, IConvertible, IFormattable, IComparable
        {
            if (IsSuccess)
                return string.Empty;

            var value = (T)Enum.Parse(typeof(T), ErrorCode.ToString());

            return string.Format(((Enum)(object)value).GetAttributeDescription(), formatValues);
        }

        public static CommandResult Success(object? response = null)
            => new CommandResult(0, null, response);

        public static CommandResult Error(int errorCode = 1, string? responseMessage = null)
        {
            if (errorCode == 0)
                throw new ArgumentException("error code cannot be zero", nameof(errorCode));

            return new CommandResult(errorCode, responseMessage, null);
        }

        internal static ErrorStatus GetErrorStatus(IEnumerable<Exception> exceptions)
        {
            return exceptions.FirstOrDefault() is MessageProcessingException ex
                ? new ErrorStatus(ex.ErrorCode, ex.Message)
                : ErrorStatus.UnknownError;
        }

        public override string ToString()
        {
            var text = new StringBuilder(IsSuccess ? "Success" : $"Error, ErrorCode: {ErrorCode}");

            if (ResponseMessage != null)
                text.Append($", ResponseMessage: [{ResponseMessage}]");

            if (Response != null)
                text.Append($", Response: [{Response}]");

            return text.ToString();
        }
    }
}
