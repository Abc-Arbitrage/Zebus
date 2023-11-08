#region (c)2009 Lokad - New BSD license

// Copyright (c) Lokad 2009
// Company: http://www.lokad.com
// This code is released under the terms of the new BSD licence

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Abc.Zebus.Util.Extensions;

/// <summary>
/// Helper related to the <see cref="Type"/>.
/// </summary>
internal static class ExtendType
{
    /// <summary>
    /// Returns single attribute from the type.
    /// </summary>
    /// <typeparam name="T">Attribute to use</typeparam>
    /// <param name="target">Attribute provider</param>
    ///<param name="inherit"><see cref="MemberInfo.GetCustomAttributes(Type,bool)"/></param>
    /// <returns><em>Null</em> if the attribute is not found</returns>
    /// <exception cref="InvalidOperationException">If there are 2 or more attributes</exception>
    public static T? GetAttribute<T>(this ICustomAttributeProvider target, bool inherit)
        where T : Attribute
    {
        if (target.IsDefined(typeof (T), inherit))
        {
            var attributes = target.GetCustomAttributes(typeof (T), inherit);
            if (attributes.Length > 1)
            {
                throw new InvalidOperationException("More than one attribute is declared");
            }
            return (T) attributes[0];
        }

        return null;
    }

    public static bool Is<T>(this Type @this)
    {
        return typeof(T).IsAssignableFrom(@this);
    }

    public static string GetPrettyName(this Type eventType)
    {
        var genericArgument = eventType.GetGenericArguments().SingleOrDefault();
        if (genericArgument == null)
            return eventType.Name;
        var cleanShortName = eventType.Name.Substring(0, eventType.Name.IndexOf('`'));
        return cleanShortName + "<" + genericArgument.Name + ">";
    }

    public static IEnumerable<Type> GetBaseTypes(this Type type)
    {
        var baseType = type.BaseType;
        while (baseType != null && baseType != typeof(object))
        {
            yield return baseType;
            baseType = baseType.BaseType;
        }
    }
}
