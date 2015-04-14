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

        private void SetPort(int value)
        {
            var portStart = Value.LastIndexOf(':') + 1;
            var endPointValue = Value.Substring(0, portStart) + value.ToString(CultureInfo.InvariantCulture);

            Value = endPointValue;
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

        private int? ReadPort(FileInfo filePath)
        {
            int port;
            if (int.TryParse(File.ReadAllText(filePath.FullName), NumberStyles.Integer, CultureInfo.InvariantCulture, out port))
                return port;

            return null;
        }
    }
}