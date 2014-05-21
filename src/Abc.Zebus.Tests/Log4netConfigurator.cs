using System;
using System.IO;
using Abc.Zebus.Util.Annotations;
using log4net.Appender;
using log4net.Config;
using log4net.Core;
using NUnit.Framework;

namespace Abc.Zebus.Tests
{
    [SetUpFixture]
    public class Log4netConfigurator
    {
        [SetUp]
        public void Setup()
        {
            var configurationFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log4net.config");
            XmlConfigurator.Configure(new FileInfo(configurationFile));
        }

        [UsedImplicitly]
        public class Appender : ConsoleAppender
        {
            protected override void Append(LoggingEvent loggingEvent)
            {
                lock (this)
                {
                    base.Append(loggingEvent);
                }
            }
        }
    }
}