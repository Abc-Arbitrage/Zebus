using System;
using System.Configuration;

namespace Abc.Zebus.Directory.Configuration
{
    public class AppSettingsBusConfiguration : IBusConfiguration
    {
        public AppSettingsBusConfiguration()
        {
            DirectoryServiceEndPoints = ConfigurationManager.AppSettings["Bus.Directory.EndPoints"].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            RegistrationTimeout = TimeSpan.Parse(ConfigurationManager.AppSettings["Bus.Directory.RegistrationTimeout"]);
            StartReplayTimeout = TimeSpan.Parse(ConfigurationManager.AppSettings["Bus.Persistence.StartReplayTimeout"]);
            IsPersistent = bool.Parse(ConfigurationManager.AppSettings["Bus.IsPersistent"]);
            IsDirectoryPickedRandomly = bool.Parse(ConfigurationManager.AppSettings["Bus.Directory.PickRandom"]);
        }

        public string[] DirectoryServiceEndPoints { get; private set; }
        public TimeSpan RegistrationTimeout { get; private set; }
        public TimeSpan StartReplayTimeout { get; private set; }
        public bool IsPersistent { get; private set; }
        public bool IsDirectoryPickedRandomly { get; private set; }
    }
}