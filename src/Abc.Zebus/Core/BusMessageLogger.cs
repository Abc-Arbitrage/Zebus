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
        private static readonly Func<Type, MessageTypeLogInfo> _logInfoFactory;
        private readonly Type _loggerType;
        private readonly ILog _logger;

        static BusMessageLogger()
        {
            _logInfoFactory = CreateLogger;
        }

        public BusMessageLogger(Type loggerType) : this(loggerType, loggerType.FullName)
        {
        }

        public BusMessageLogger(Type loggerType, string loggerFullName)
        {
            _loggerType = loggerType;
            _logger = LogManager.GetLogger(loggerFullName);
        }

        public bool IsInfoEnabled(IMessage message)
        {
            var logInfo = GetLogInfo(message);
            return logInfo.Logger.IsInfoEnabled;
        }

        [StringFormatMethod("format")]
        public void InfoFormat(string format, IMessage message, MessageId? messageId = null, int messageSize = 0, PeerId peerId = default(PeerId))
        {
            var logInfo = GetLogInfo(message);
            if (!logInfo.Logger.IsInfoEnabled)
                return;

            var messageText = logInfo.GetMessageText(message);
            _logger.InfoFormat(format, messageText, messageId, messageSize, peerId);
        }

        [StringFormatMethod("format")]
        public void DebugFormat(string format, IMessage message, MessageId? messageId = null, int messageSize = 0, PeerId peerId = default(PeerId))
        {
            var logInfo = GetLogInfo(message);
            if (!logInfo.Logger.IsDebugEnabled)
                return;

            var messageText = logInfo.GetMessageText(message);
            _logger.DebugFormat(format, messageText, messageId, messageSize, peerId);
        }

        [StringFormatMethod("format")]
        public void InfoFormat(string format, IMessage message, MessageId messageId, int messageSize, IList<Peer> peers, Level logLevel = null)
        {
            if (peers.Count == 0)
            {
                InfoFormat(format, message, messageId, messageSize);
                return;
            }
            if (peers.Count == 1)
            {
                InfoFormat(format, message, messageId, messageSize, peerId: peers[0].Id);
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
        {
            var logInfo = GetLogInfo(message);
            return logInfo.GetMessageText(message);
        }

        private static MessageTypeLogInfo GetLogInfo(IMessage message)
        {
            return _logInfos.GetOrAdd(message.GetType(), _logInfoFactory);
        }

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
                return _hasToStringOverride ? string.Format("{0} {{{1}}}", _messageTypeName, message) : string.Format("{0}", _messageTypeName);
            }
        }
    }
}