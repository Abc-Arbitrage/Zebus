using System;
using System.ComponentModel;
using Abc.Zebus.Util.Extensions;

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

            return string.Format(GetDescriptionFromEnum<T>(value), formatValues);
        }

        private string GetDescriptionFromEnum<T>(T value) where T : struct, IConvertible, IFormattable, IComparable
        {
            var memInfo = typeof(T).GetMember(value.ToString());

            var descAttr = memInfo[0].GetAttribute<DescriptionAttribute>(false);
            if (descAttr == null)
                return string.Empty;

            return descAttr.Description;
        }
    }
}
