using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Abc.Zebus.Util;
using NUnit.Framework;

namespace Abc.Zebus.Testing.Integration
{
    public class TestService : IDisposable
    {
        private Process? _process;
        private TextWriter _outputWriter = Console.Out;
        private TextWriter _errorWriter = Console.Error;
        private string _serviceName;
        private readonly string _configurationFile;
        private readonly string _buildFile;
        private Mutex? _stopMutex;
        private bool _isMutexReleased;
        private string _buildDirectory;
        const string _hostFileName = "Abc.Zebus.Host.exe";
        private const string _tempFolder = @"C:\Dev\integration_tests";

        public bool RedirectOutput { get; set; }

        public TestService(string serviceName, string configurationFile, string buildFile)
        {
            if (!System.IO.Directory.Exists(_tempFolder))
                System.IO.Directory.CreateDirectory(_tempFolder);
            _buildDirectory = Path.Combine(_tempFolder, Guid.NewGuid().ToString());
            _serviceName = serviceName;
            _configurationFile = configurationFile;
            _buildFile = buildFile;

            RedirectOutput = true;

            if(!File.Exists(configurationFile))
                throw new ArgumentException("Unknown configuration file: " + configurationFile + ". Make sure that \"Copy to output directory\" is set.");

            if(!File.Exists(buildFile))
                throw new ArgumentException("Unknown build file: " + buildFile);
        }

        public void Build()
        {
            LogInfo("Building service in \"" + _buildDirectory + "\"");
            System.IO.Directory.CreateDirectory(_buildDirectory);
            var process = new Process
            {
                StartInfo = new ProcessStartInfo(IntegrationTestFixture.GetPathFromRepositoryBase(@"tools\nant\nant.exe"))
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = RedirectOutput,
                    RedirectStandardInput = RedirectOutput,
                    RedirectStandardError = RedirectOutput,
                    CreateNoWindow = true,
                    Arguments = "-buildfile:" + Path.GetFileName(_buildFile) + " build-only -D:build.dir=\"" + _buildDirectory + "\"",
                    WorkingDirectory = Path.GetDirectoryName(_buildFile),
                }
            };

            process.ErrorDataReceived += (sender, args) => LogError(args.Data);
            process.OutputDataReceived += (sender, args) => LogInfo(args.Data);

            process.Start();

            if(RedirectOutput)
                process.BeginOutputReadLine();

            process.WaitForExit();
            LogInfo("Build complete");

            File.Copy(_configurationFile, Path.Combine(_buildDirectory, "Abc.Zebus.Host.exe.config"), true);
            LogInfo("Config file copied");
        }

        public void Start()
        {
            LogInfo("Starting " + _serviceName);

            var mutexName = _serviceName + "." + Guid.NewGuid().ToString().Substring(0, 6);
            CreateMutex(mutexName);

            _process = new Process
            {
                StartInfo = new ProcessStartInfo(Path.Combine(_buildDirectory, _hostFileName))
                {
                    WorkingDirectory = _buildDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = RedirectOutput,
                    RedirectStandardInput = RedirectOutput,
                    RedirectStandardError = RedirectOutput,
                    CreateNoWindow = true,
                    Arguments = "/MutexName:" + mutexName
                }
            };

            _process.ErrorDataReceived += (sender, args) => LogError(args.Data);
            _process.OutputDataReceived += (sender, args) => LogInfo(args.Data);
            _process.Start();

            if (RedirectOutput)
                _process.BeginOutputReadLine();
        }

        private void LogError(string text)
        {
            _errorWriter.WriteLine("[{0:HH:mm:ss.fff}][{1}Manager] {2}", DateTime.Now, _serviceName, text);
        }

        private void LogInfo(string text)
        {
            _outputWriter.WriteLine("[{0:HH:mm:ss.fff}][{1}Manager] {2}", DateTime.Now, _serviceName, text);
        }

        public void Stop()
        {
            Stop(50.Seconds());
        }

        public void Stop(TimeSpan timeout)
        {
            LogInfo("Closing service");
            ReleaseMutex();

            if (_process != null && !_process.WaitForExit((int)timeout.TotalMilliseconds))
                Assert.Fail(_serviceName + " did not exit properly");
        }

        private void CreateMutex(string mutexName)
        {
            _stopMutex = new Mutex(true, mutexName);
            _isMutexReleased = false;
        }

        private void ReleaseMutex()
        {
            if (_stopMutex == null || _isMutexReleased)
                return;

            _stopMutex.ReleaseMutex();
            _stopMutex.Dispose();
            _isMutexReleased = true;
        }

        public void Kill()
        {
            if (_process is null)
                return;

            LogInfo("Killing service");
            _process.Kill();
        }

        public void Dispose()
        {
            LogInfo("Disposing service");
            System.IO.Directory.Delete(_buildDirectory, true);
        }

        ~TestService()
        {
            ReleaseMutex();
        }
    }
}
