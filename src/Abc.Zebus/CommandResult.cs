using Abc.Zebus.Util.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Abc.Zebus
{
    public class CommandResult
    {
        public CommandResult(int errorCode, object response)
        {
            ErrorCode = errorCode;
            Response = response;
        }

        public int ErrorCode { get; private set; }
        public object Response { get; private set; }

        public bool IsSuccess
        {
            get { return ErrorCode == 0; }
        }

        public string GetErrorMessageFromEnum<T>(params object[] formatValues) where T : struct, IConvertible, IFormattable, IComparable
        {
            if (IsSuccess)
                return string.Empty;

            var value = (T)Enum.Parse(typeof(T), ErrorCode.ToString());

            return string.Format(((Enum)(object)value).GetAttributeDescription(), formatValues);
        }

        internal static int GetErrorCode(IEnumerable<Exception> exceptions)
        {
            var domainException = exceptions.FirstOrDefault() as DomainException;
            return domainException != null ? domainException.ErrorCode : 1;
        }
    }
}
