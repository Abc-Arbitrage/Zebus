using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Abc.Zebus.Util.Annotations;
using Abc.Zebus.Util.Extensions;
using log4net;
using log4net.Core;

namespace Abc.Zebus.Core
{
    public class BusMessageLogger
    {
        private static readonly ConcurrentDictionary<Type, MessageTypeLogInfo> _logInfos = new ConcurrentDictionary<Type, MessageTypeLogInfo>();
        private static readonly Func<Type, MessageTypeLogInfo> _logInfoFactory = CreateLogger;
        private readonly Type _loggerType;
        private readonly ILog _logger;
        private bool _logDebugEnabled;
        private bool _logInfoEnabled;

        public BusMessageLogger(Type loggerType)
            : this(loggerType, loggerType.FullName)
        {
        }

        public BusMessageLogger(Type loggerType, string loggerFullName)
        {
            _loggerType = loggerType;
            _logger = LogManager.GetLogger(loggerFullName);

            // Instances of BusMessageLogger are static, no need to unsubscribe from these events
            _logger.Logger.Repository.ConfigurationChanged += (sender, args) => UpdateLogConfig();
            _logger.Logger.Repository.ConfigurationReset += (sender, args) => UpdateLogConfig();
            UpdateLogConfig();

            void UpdateLogConfig()
            {
                _logDebugEnabled = _logger.IsDebugEnabled;
                _logInfoEnabled = _logger.IsInfoEnabled;
            }
        }

        public bool IsInfoEnabled(IMessage message)
            => _logInfoEnabled && GetLogInfo(message).Logger.IsInfoEnabled;

        [StringFormatMethod("format")]
        public void InfoFormat(string format, IMessage message, MessageId? messageId = null, long messageSize = 0, PeerId peerId = default(PeerId))
        {
            if (!_logInfoEnabled)
                return;

            var logInfo = GetLogInfo(message);
            if (!logInfo.Logger.IsInfoEnabled)
                return;

            var messageText = logInfo.GetMessageText(message);
            _logger.InfoFormat(format, messageText, messageId, messageSize, peerId);
        }

        [StringFormatMethod("format")]
        public void DebugFormat(string format, IMessage message, MessageId? messageId = null, long messageSize = 0, PeerId peerId = default(PeerId))
        {
            if (!_logDebugEnabled)
                return;

            var logInfo = GetLogInfo(message);
            if (!logInfo.Logger.IsDebugEnabled)
                return;

            var messageText = logInfo.GetMessageText(message);
            _logger.DebugFormat(format, messageText, messageId, messageSize, peerId);
        }

        [StringFormatMethod("format")]
        public void InfoFormat(string format, IMessage message, MessageId messageId, long messageSize, IList<Peer> peers, Level logLevel = null)
        {
            if (!_logInfoEnabled)
                return;

            switch (peers.Count)
            {
                case 0:
                    InfoFormat(format, message, messageId, messageSize);
                    return;

                case 1:
                    InfoFormat(format, message, messageId, messageSize, peers[0].Id);
                    return;
            }

            var logInfo = GetLogInfo(message);
            if (!logInfo.Logger.IsInfoEnabled)
                return;

            var messageText = logInfo.GetMessageText(message);
            var otherPeersCount = peers.Count - 1;
            var peerIdText = otherPeersCount > 1
                ? peers[0].Id + " and " + otherPeersCount + " other peers"
                : peers[0].Id + " and " + otherPeersCount + " other peer";

            _logger.Logger.Log(_loggerType, logLevel ?? Level.Info, string.Format(format, messageText, messageId, messageSize, peerIdText), null);
        }

        public static string ToString(IMessage message)
            => GetLogInfo(message).GetMessageText(message);

        private static MessageTypeLogInfo GetLogInfo(IMessage message)
            => _logInfos.GetOrAdd(message.GetType(), _logInfoFactory);

        private static MessageTypeLogInfo CreateLogger(Type messageType)
        {
            var logger = LogManager.GetLogger(messageType);
            var hasToStringOverride = HasToStringOverride(messageType);

            return new MessageTypeLogInfo(logger, hasToStringOverride, messageType.GetPrettyName());
        }

        private static bool HasToStringOverride(Type messageType)
        {
            var methodInfo = messageType.GetMethod("ToString", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            return methodInfo != null;
        }

        private class MessageTypeLogInfo
        {
            public readonly ILog Logger;
            private readonly bool _hasToStringOverride;
            private readonly string _messageTypeName;

            public MessageTypeLogInfo(ILog logger, bool hasToStringOverride, string messageTypeName)
            {
                Logger = logger;
                _hasToStringOverride = hasToStringOverride;
                _messageTypeName = messageTypeName;
            }

            public string GetMessageText(IMessage message)
            {
                return _hasToStringOverride ? $"{_messageTypeName} {{{message}}}" : $"{_messageTypeName}";
            }
        }
    }
}
