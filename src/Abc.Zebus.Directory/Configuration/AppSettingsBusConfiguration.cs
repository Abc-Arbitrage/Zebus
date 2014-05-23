using System;
using System.Configuration;

namespace Abc.Zebus.Directory.Configuration
{
    public class AppSettingsBusConfiguration : IBusConfiguration
    {
        private static IBusConfiguration _instance;

        public string[] DirectoryServiceEndPoints { get; set; }
        public TimeSpan RegistrationTimeout { get; private set; }
        public TimeSpan StartReplayTimeout { get; private set; }
        public bool IsPersistent { get; private set; }
        public bool IsDirectoryPickedRandomly { get; private set; }

        private AppSettingsBusConfiguration()
        {
            DirectoryServiceEndPoints = ConfigurationManager.AppSettings["Bus.Directory.EndPoints"].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            RegistrationTimeout = TimeSpan.Parse(ConfigurationManager.AppSettings["Bus.Directory.RegistrationTimeout"]);
            StartReplayTimeout = TimeSpan.Parse(ConfigurationManager.AppSettings["Bus.Persistence.StartReplayTimeout"]);
            IsPersistent = bool.Parse(ConfigurationManager.AppSettings["Bus.IsPersistent"]);
            IsDirectoryPickedRandomly = bool.Parse(ConfigurationManager.AppSettings["Bus.Directory.PickRandom"]);
        }

        public static IBusConfiguration Current
        {
            get { return _instance ?? (_instance = new AppSettingsBusConfiguration()); }
        }
    }
}