using System;
using Abc.Zebus.Util;

namespace Abc.Zebus.Directory.Configuration
{
    public class AppSettingsBusConfiguration : IBusConfiguration
    {
        public AppSettingsBusConfiguration()
        {
            RegistrationTimeout = AppSettings.Get("Bus.Directory.RegistrationTimeout", 30.Seconds());
            StartReplayTimeout = AppSettings.Get("Bus.Persistence.StartReplayTimeout", 30.Seconds());
            IsDirectoryPickedRandomly = AppSettings.Get("Bus.Directory.PickRandom", true);
            IsErrorPublicationEnabled = AppSettings.Get("Bus.IsErrorPublicationEnabled", true);
            MessagesBatchSize = AppSettings.Get("Bus.MessagesBatchSize", 100);
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