using System;
using System.ComponentModel;

namespace Abc.Zebus.Util.Extensions
{
    public static class ExtendEnum
    {
        public static T GetAttributeOfType<T>(this Enum enumVal) where T : Attribute
        {
            var type = enumVal.GetType();
            var memInfo = type.GetMember(enumVal.ToString());

            return memInfo[0].GetAttribute<T>(false);
        }

        public static string GetAttributeDescription(this Enum enumValue)
        {
            var attribute = enumValue.GetAttributeOfType<DescriptionAttribute>();

            return attribute == null ? String.Empty : attribute.Description;
        }
    }
}
