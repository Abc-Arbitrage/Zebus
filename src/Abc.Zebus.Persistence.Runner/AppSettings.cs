using System;
using System.Configuration;
using System.Globalization;

namespace Abc.Zebus.Persistence.Runner
{
    internal static class AppSettings
    {
        public static T Get<T>(string key, T defaultValue)
        {
            var value = ConfigurationManager.AppSettings[key];
            if (value == null)
                return defaultValue;

            return Parser<T>.Parse(value);
        }

        public static string[] GetArray(string key)
        {
            var value = ConfigurationManager.AppSettings[key];
            if (value == null)
                return new string[0];

            return value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static class Parser<T>
        {
            private static readonly Func<string, object> _value;

            public static T Parse(string s)
            {
                return (T)_value(s);
            }

            static Parser()
            {
                if (typeof(T) == typeof(TimeSpan))
                    _value = s => TimeSpan.Parse(s, CultureInfo.InvariantCulture);
                else
                    _value = s => Convert.ChangeType(s, typeof(T));
            }
        }
    }
}