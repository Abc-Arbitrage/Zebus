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

        public string[] DirectoryServiceEndPoints { get { return new string[0]; } }
        public bool IsPersistent { get { return false; } }

        public TimeSpan RegistrationTimeout { get; private set; }
        public TimeSpan StartReplayTimeout { get; private set; }
        public bool IsDirectoryPickedRandomly { get; private set; }
        public bool IsErrorPublicationEnabled { get; private set; }
        public int MessagesBatchSize { get; private set; }
    }
}
