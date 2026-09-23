using System;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;
using Abc.Zebus.Util.Extensions;

namespace Abc.Zebus;

[Serializable]
public class DomainException : MessageProcessingException
{
    public DomainException(Exception? innerException, int errorCode, string message)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    public DomainException(string message)
        : this(null, ErrorStatus.UnknownError.Code, message)
    {
    }

    public DomainException(string message, Exception? innerException)
        : this(innerException, ErrorStatus.UnknownError.Code, message)
    {
    }

    public DomainException(int errorCode, string message)
        : this(null, errorCode, message)
    {
    }

    public DomainException(int errorCode, string message, params object[] values)
        : this(errorCode, string.Format(message, values))
    {
    }

    public DomainException(Enum enumVal, params object[] values)
        : this(Convert.ToInt32(enumVal), enumVal.GetAttributeDescription(), values)
    {
    }

    public DomainException(Expression<Func<int>> errorCodeExpression, params object[] values)
        : this(errorCodeExpression.Compile()(), ReadDescriptionFromAttribute(errorCodeExpression), values)

    {
    }

#pragma warning disable SYSLIB0051
    protected DomainException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
#pragma warning restore SYSLIB0051

    private static string ReadDescriptionFromAttribute(Expression<Func<int>> errorCodeExpression)
    {
        var memberExpr = errorCodeExpression.Body as MemberExpression;
        if (memberExpr == null)
            return string.Empty;

        var attr = (DescriptionAttribute?)memberExpr.Member.GetCustomAttributes(typeof(DescriptionAttribute)).FirstOrDefault();

        return attr != null ? attr.Description : string.Empty;
    }
}
