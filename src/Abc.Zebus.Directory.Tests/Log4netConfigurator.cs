using System;
using System.IO;
using log4net.Config;
using log4net.Core;
using NUnit.Framework;

namespace Abc.Zebus.Directory.Tests
{
    [SetUpFixture]
    public class Log4netConfigurator
    {
        [OneTimeSetUp]
        public void SetUp()
        {
            var configurationFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log4net.config");
            XmlConfigurator.Configure(LoggerManager.GetRepository(typeof(Log4netConfigurator).Assembly), new FileInfo(configurationFile));
        }
    }
}
