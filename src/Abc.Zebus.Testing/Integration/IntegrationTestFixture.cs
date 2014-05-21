using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading;
using Abc.Zebus.Core;
using Abc.Zebus.Util;
using NUnit.Framework;

namespace Abc.Zebus.Testing.Integration
{
    [TestFixture]
    public abstract class IntegrationTestFixture
    {
        private IBus _controlBus;
        private static TimeSpan _controlBusActionsTimeout = 40.Seconds();
        private TestService _directoryServiceController;
        private Stopwatch _testStopwatch;

        [SetUp]
        public void Setup()
        { 
            KillInstance(@"\\integration_tests");
            _testStopwatch = Stopwatch.StartNew();

            _directoryServiceController = CreateDirectoryServiceController();
            _directoryServiceController.Build();
            _directoryServiceController.Start();

            _controlBus = CreateAndStartControlBus();
        }

        [TearDown]
        public void Teardown()
        {
            _controlBus.Stop();
            _directoryServiceController.Stop();
            _directoryServiceController.Dispose();
            
            Log("Integration test lasted " + _testStopwatch.Elapsed);
            KillInstance(@"\\integration_tests");
        }

        private TestService CreateDirectoryServiceController()
        {
            const string serviceName = "Abc.Zebus.DirectoryService";
            var configFile = PathUtil.InCurrentNamespaceDirectory(@"Configurations\Directory-Local.config");
            var buildFile = GetPathFromRepositoryBase(@"src\Abc.Zebus\Abc.Zebus.DirectoryService\Abc.Zebus.Directory.build");

            return new TestService(serviceName, configFile, buildFile) { RedirectOutput = false };
        }

        public MessageWaiter<TMessage> ListenForMessageMatchingCondition<TMessage>(Func<TMessage, bool> desiredCondition) where TMessage : class, IMessage
        {
            return new MessageWaiter<TMessage>(_controlBus, desiredCondition);
        }

        protected static IBus CreateAndStartSenderBus()
        {
            return new BusFactory().WithConfiguration("tcp://localhost:129", "Local")
                                   .WithWaitForEndOfStreamAckTimeout(500.Milliseconds())
                                   .CreateAndStartBus();
        }

        protected static IBus CreateAndStartControlBus()
        {
            // Unobstrusive way of making the bus retry the register process
            var directoryEndPoint = "tcp://localhost:129 tcp://localhost:129 tcp://localhost:129";
            return new BusFactory().WithConfiguration(directoryEndPoint, "Local")
                                   .WithPeerId("Abc.ControlBus." + Guid.NewGuid().ToString().Substring(0, 6))
                                   .WithWaitForEndOfStreamAckTimeout(500.Milliseconds())
                                   .CreateAndStartBus();
        }

        protected static void Log(string text)
        {
            Console.WriteLine("[{0:HH:mm:ss.fff}][IntegrationTestController] {1}", DateTime.Now, text);
        }

        protected void NinjaLog(string text)
        {
            Console.WriteLine(@"
      ___                                                             
     /___\_/                                                          
     |\_/|<\                              
     (`o`) `   __(\_            |\_    " + DateTime.Now.ToString("HH:mm:ss.fff") + @"                               
     \ ~ /_.-`` _|__)  ( ( ( ( /()/    " + text + @"                                
    _/`-`  _.-``               `\|  
 .-`      (    .-.                                                    
(   .-     \  /   `-._                                                
 \  (\_    /\/        `-.__-()                                        
  `-|__)__/ /  /``-.   /_____8                                        
        \__/  /     `-`                                               
       />|   /                                                        
      /| J   L                                                        
      `` |   |                                                        
         L___J                                                        
          ( |                       
         .oO() 
______________________________________________________________________________                                                       
");
        }

        public class MessageWaiter<TMessage> : IDisposable where TMessage : class, IMessage
        {
            private IDisposable _subscription;
            private ManualResetEvent _conditionHappened = new ManualResetEvent(false);

            public MessageWaiter(IBus bus, Func<TMessage, bool> desiredMessageCondition)
            {
                _subscription = bus.Subscribe<TMessage>(msg =>
                {
                    if (desiredMessageCondition(msg))
                        _conditionHappened.Set();
                });
            }

            public void Wait()
            {
                if (!_conditionHappened.WaitOne(_controlBusActionsTimeout))
                    throw new TimeoutException("The desired condition was not met in the allotted time.");
            }

            public void Dispose()
            {
                _subscription.Dispose();
            }
        }

        public static string GetPathFromRepositoryBase(string relativeFilePath)
        {
            var srcDirName = "\\src\\";
            var currentDir = PathUtil.InBaseDirectory();
            var position = 0;

            for (int i = 1; i < 10; i++)
            {
                position = currentDir.IndexOf(srcDirName, position, StringComparison.Ordinal);
                var srcDir = currentDir.Substring(0, position);
                if (File.Exists(Path.Combine(srcDir, @".hgignore")))
                    return Path.Combine(srcDir, relativeFilePath);

                position += srcDirName.Length;
            }
            
            throw new Exception();
        }

        private static void KillInstance(string serviceFolder)
        {
            KillInstance(serviceFolder, Environment.MachineName);
        }

        private static void KillInstance(string serviceFolder, string machineName)
        {
            try
            {
                var managementScope = new ManagementScope(@"\\" + machineName + @"\ROOT\CIMV2", new ConnectionOptions());
                managementScope.Connect();

                var query = string.Format(@"SELECT Handle FROM Win32_Process WHERE Name = 'Abc.Zebus.Host.exe' AND ExecutablePath LIKE '%{0}%'", serviceFolder);
                var searcher = new ManagementObjectSearcher(managementScope, new ObjectQuery(query));

                var processes = searcher.Get().Cast<ManagementObject>().ToList();

                for (int i = 0; i < processes.Count; i++)
                {
                    var process = processes[i];
                    if (process != null)
                    {
                        process.InvokeMethod("Terminate", null);
                        Console.WriteLine("Process #{0} on {1} - KILLED", i, machineName);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: Cannot kill process on machine {0}", machineName);
                Console.WriteLine(ex);
                Console.ReadLine();
            }
        }
    }
}