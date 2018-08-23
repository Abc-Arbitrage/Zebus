using System.Collections.Specialized;
using Abc.Zebus.Directory.Configuration;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util;
using NUnit.Framework;

namespace Abc.Zebus.Directory.Tests.Configuration
{
    [TestFixture]
    public class ConfigurationTests
    {
        private NameValueCollection _appSettings;

        [SetUp]
        public void SetUp()
        {
            _appSettings = new NameValueCollection();
        }

        [Test]
        public void should_read_registration_timeout()
        {
            SetAppSettingsKey("Bus.Directory.RegistrationTimeout", "00:00:42");

            var busConfiguration = new AppSettingsBusConfiguration(new AppSettings(_appSettings));
            busConfiguration.RegistrationTimeout.ShouldEqual(42.Seconds());
        }

        [Test]
        public void should_read_default_registration_timeout()
        {
            var busConfiguration = new AppSettingsBusConfiguration(new AppSettings(_appSettings));
            busConfiguration.RegistrationTimeout.ShouldEqual(30.Seconds());
        }
        
        [Test]
        public void should_read_peer_ping_interval()
        {
            SetAppSettingsKey("Directory.PingPeers.Interval", "00:02");

            var directoryConfiguration = new AppSettingsDirectoryConfiguration(new AppSettings(_appSettings));
            directoryConfiguration.PeerPingInterval.ShouldEqual(2.Minutes());
        }

        [Test]
        public void should_read_default_max_allowed_clock_diff()
        {
            var directoryConfiguration = new AppSettingsDirectoryConfiguration(new AppSettings(_appSettings));
            directoryConfiguration.MaxAllowedClockDifferenceWhenRegistering.ShouldEqual(null);
        }

        [Test]
        public void should_read_max_allowed_clock_diff()
        {
            SetAppSettingsKey("Directory.MaxAllowedClockDifferenceWhenRegistering", "00:02");

            var directoryConfiguration = new AppSettingsDirectoryConfiguration(new AppSettings(_appSettings));
            directoryConfiguration.MaxAllowedClockDifferenceWhenRegistering.ShouldEqual(2.Minutes());
        }

        [Test]
        public void should_read_default_peer_ping_interval()
        {
            var directoryConfiguration = new AppSettingsDirectoryConfiguration(new AppSettings(_appSettings));
            directoryConfiguration.PeerPingInterval.ShouldEqual(1.Minute());
        }

        private void SetAppSettingsKey(string key, string value)
        {
            _appSettings[key] = value;
        }
    }
}
