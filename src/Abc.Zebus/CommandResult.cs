using System;
using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Util.Extensions;

namespace Abc.Zebus
{
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

        internal static ErrorStatus GetErrorStatus(IEnumerable<Exception> exceptions)
        {
            return exceptions.FirstOrDefault() is MessageProcessingException ex
                ? new ErrorStatus(ex.ErrorCode, ex.Message)
                : ErrorStatus.UnknownError;
        }
    }
}
