using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Abc.Zebus.Util;
using Abc.Zebus.Util.Extensions;
using log4net;

namespace Abc.Zebus.Transport
{
    public class ZmqEndPoint
    {
        private const string _dataDirectory = "Data";
        private readonly ILog _logger = LogManager.GetLogger(typeof(ZmqEndPoint));

        public ZmqEndPoint(string value)
        {
            Value = CleanEndPoint(value);
        }

        public string Value { get; private set; }

        public bool HasRandomPort
        {
            get { return Value.EndsWith(":*"); }
        }

        public int GetPort()
        {
            var portStart = Value.LastIndexOf(':') + 1;
            var portPart = Value.Substring(portStart);
            return int.Parse(portPart, CultureInfo.InvariantCulture);
        }

        public bool SetPortIfAvailable(int port)
        {
            if (!TcpUtil.IsPortUnused(port))
            {
                _logger.WarnFormat("Specified port {0} is unavailable", port);
                return false;
            }

            SetPort(port);
            return true;
        }

        private void SetPort(int value)
        {
            var portStart = Value.LastIndexOf(':') + 1;
            var endPointValue = Value.Substring(0, portStart) + value.ToString(CultureInfo.InvariantCulture);

            Value = endPointValue;
        }

        public void SavePort(PeerId peerId, string environment)
        {
            var filePath = GetTargetPortFilePath(peerId, environment);
            File.WriteAllText(filePath, GetPort().ToString());
        }

        private bool LoadPreviousPortIfAvailable(PeerId peerId, string environment)
        {
            var filePath = GetPortFilePath(peerId, environment);

            if (!File.Exists(filePath))
                return false;

            int port;
            if (!int.TryParse(File.ReadAllText(filePath), NumberStyles.Integer, CultureInfo.InvariantCulture, out port))
                return false;

            _logger.InfoFormat("Trying to use port {0} specified in inboundport file", port);

            return SetPortIfAvailable(port);
        }

        private static string GetTargetPortFilePath(PeerId peerId, string environment)
        {
            var dataDir = PathUtil.InBaseDirectory(_dataDirectory);
            if (!System.IO.Directory.Exists(dataDir))
                System.IO.Directory.CreateDirectory(dataDir);
            return Path.Combine(dataDir, peerId + ".inboundport." + environment);
        }

        private static string GetPortFilePath(PeerId peerId, string environment)
        {
            var portFileInDataDir = Path.Combine(PathUtil.InBaseDirectory("Data"), peerId + ".inboundport." + environment);
            if (File.Exists(portFileInDataDir))
                return portFileInDataDir;
            return PathUtil.InBaseDirectory(peerId + ".inboundport." + environment);
        }

        private static string CleanEndPoint(string value)
        {
            if (value == null)
                return "tcp://*:*";

            return value.Replace("0.0.0.0", Environment.MachineName).ToLower();
        }

        /// <remarks>
        /// Selecting a random port is more complicated than just usint tcp://*.* because we want to avoid using a port 
        /// that was used for another environment. If we were to share a port between two environments, clients from the
        /// second environment might try to send us messages that we would have to discard
        /// </remarks>>
        public void SelectRandomPort(PeerId peerId, string environment)
        {
            if (!LoadPreviousPortIfAvailable(peerId, environment))
            {
                _logger.InfoFormat("Selecting random port for {0}", environment);

                var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                var forbiddenPorts = directory.GetFiles("*.inboundport.*").Select(ReadPort).Where(port => port.HasValue).ToHashSet();

                if(forbiddenPorts.Any())
                    _logger.InfoFormat("Ports already reserved for other environments: {0}", string.Join(", ", forbiddenPorts));

                int? selectedPort = null;
                do
                {
                    var port = TcpUtil.GetRandomUnusedPort();
                    if (!forbiddenPorts.Contains(port))
                        selectedPort = port;
                } while (!selectedPort.HasValue);

                SetPort(selectedPort.Value);
            }
        }

        private int? ReadPort(FileInfo filePath)
        {
            int port;
            if (int.TryParse(File.ReadAllText(filePath.FullName), NumberStyles.Integer, CultureInfo.InvariantCulture, out port))
                return port;

            return null;
        }
    }
}