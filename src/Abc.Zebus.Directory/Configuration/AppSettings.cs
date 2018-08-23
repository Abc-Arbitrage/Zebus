using System;
using System.Collections.Specialized;
using System.Configuration;
using System.Globalization;

namespace Abc.Zebus.Directory.Configuration
{
    internal class AppSettings
    {
        private readonly NameValueCollection _appSettings;

        public AppSettings()
            :this(ConfigurationManager.AppSettings)
        {
        }

        public AppSettings(NameValueCollection appSettings)
        {
            _appSettings = appSettings;
        }

        public T Get<T>(string key, T defaultValue)
        {
            var value = _appSettings[key];
            if (value == null)
                return defaultValue;

            return Parser<T>.Parse(value);
        }

        public string[] GetArray(string key)
        {
            var value = _appSettings[key];
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
                var conversionType = typeof(T);
                if (conversionType.IsGenericType && conversionType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    conversionType = conversionType.GenericTypeArguments[0];

                if (conversionType == typeof(TimeSpan))
                    _value = s => TimeSpan.Parse(s, CultureInfo.InvariantCulture);
                else
                    _value = s => Convert.ChangeType(s, conversionType);
            }
        }
    }
}
