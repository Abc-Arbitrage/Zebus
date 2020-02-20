using System;
using System.ComponentModel;

namespace Abc.Zebus.Util.Extensions
{
    internal static class ExtendEnum
    {
        public static string GetAttributeDescription(this Enum enumValue)
        {
            var attribute = enumValue.GetAttributeOfType<DescriptionAttribute>();
            return attribute == null ? string.Empty : attribute.Description;
        }

        private static T? GetAttributeOfType<T>(this Enum enumVal)
            where T : Attribute
        {
            var enumType = enumVal.GetType();
            var memberInfo = enumType.GetMember(enumVal.ToString());

            return memberInfo?[0].GetAttribute<T>(false);
        }
    }
}
