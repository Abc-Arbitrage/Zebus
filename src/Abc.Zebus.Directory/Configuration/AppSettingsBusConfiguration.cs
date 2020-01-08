using System;
using Abc.Zebus.Util;

namespace Abc.Zebus.Directory.Configuration
{
    public class AppSettingsBusConfiguration : IBusConfiguration
    {
        public AppSettingsBusConfiguration()
            : this(new AppSettings())
        {
        }

        internal AppSettingsBusConfiguration(AppSettings appSettings)
        {
            RegistrationTimeout = appSettings.Get("Bus.Directory.RegistrationTimeout", 30.Seconds());
            StartReplayTimeout = appSettings.Get("Bus.Persistence.StartReplayTimeout", 30.Seconds());
            IsDirectoryPickedRandomly = appSettings.Get("Bus.Directory.PickRandom", true);
            IsErrorPublicationEnabled = appSettings.Get("Bus.IsErrorPublicationEnabled", true);
            MessagesBatchSize = appSettings.Get("Bus.MessagesBatchSize", 100);
        }

        public string[] DirectoryServiceEndPoints => Array.Empty<string>();
        public bool IsPersistent => false;

        public TimeSpan RegistrationTimeout { get; }
        public TimeSpan StartReplayTimeout { get; }
        public bool IsDirectoryPickedRandomly { get; }
        public bool IsErrorPublicationEnabled { get; }
        public int MessagesBatchSize { get; }
    }
}
