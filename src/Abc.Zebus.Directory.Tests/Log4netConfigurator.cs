using System;
using System.IO;
using log4net.Config;
using NUnit.Framework;

namespace Abc.Zebus.Directory.Tests
{
    [SetUpFixture]
    public class Log4netConfigurator
    {
        [SetUp]
        public void SetUp()
        {
            var configurationFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log4net.config");
            XmlConfigurator.Configure(new FileInfo(configurationFile));
        }
    }
}