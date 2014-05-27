using System;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Abc.Zebus
{
    public class DomainException : Exception
    {
        public int ErrorCode { get; private set; }

        public DomainException(int errorCode, string message, params object[] values)
            : base(string.Format(message, values))
        {
            ErrorCode = errorCode;
        }

        public DomainException(Expression<Func<int>> errorCodeExpression, params object[] values)
            : base(string.Format(ReadDescriptionFromAttribute(errorCodeExpression), values))
        {
            ErrorCode = errorCodeExpression.Compile()();
        }

        static string ReadDescriptionFromAttribute(Expression<Func<int>> errorCodeExpression)
        {
            var memberExpr = errorCodeExpression.Body as MemberExpression;
            if (memberExpr == null)
                return string.Empty;

            var attr = (DescriptionAttribute)memberExpr.Member.GetCustomAttributes(typeof(DescriptionAttribute)).FirstOrDefault();

            return attr != null ? attr.Description : string.Empty;
        }
    }
}