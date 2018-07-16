using System.Configuration;
using Abc.Zebus.Directory.Configuration;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util;
using NUnit.Framework;

namespace Abc.Zebus.Directory.Tests.Configuration
{
    [TestFixture]
    public class ConfigurationTests
    {
        [Test]
        public void should_read_registration_timeout()
        {
            SetAppSettingsKey("Bus.Directory.RegistrationTimeout", "00:00:42");

            var configuration = new AppSettingsBusConfiguration();
            configuration.RegistrationTimeout.ShouldEqual(42.Seconds());
        }

        [Test]
        public void should_read_default_registration_timeout()
        {
            RemoveAppSettingsKey("Bus.Directory.RegistrationTimeout");

            var configuration = new AppSettingsBusConfiguration();
            configuration.RegistrationTimeout.ShouldEqual(30.Seconds());
        }
        
        [Test]
        public void should_read_peer_ping_interval()
        {
            SetAppSettingsKey("Directory.PingPeers.Interval", "00:02");

            var configuration = new AppSettingsDirectoryConfiguration();
            configuration.PeerPingInterval.ShouldEqual(2.Minutes());
        }

        [Test]
        public void should_read_default_peer_ping_interval()
        {
            RemoveAppSettingsKey("Directory.PingPeers.Interval");

            var configuration = new AppSettingsDirectoryConfiguration();
            configuration.PeerPingInterval.ShouldEqual(1.Minute());
        }

        private static void SetAppSettingsKey(string key, string value)
        {
            var appConfig = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var element = appConfig.AppSettings.Settings[key];
            if (element == null)
                appConfig.AppSettings.Settings.Add(key, value);
            else
                element.Value = value;

            appConfig.Save();

            ConfigurationManager.RefreshSection("appSettings");
        }

        private static void RemoveAppSettingsKey(string key)
        {
            var appConfig = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            appConfig.AppSettings.Settings.Remove(key);
            appConfig.Save();

            ConfigurationManager.RefreshSection("appSettings");
        }
    }
}