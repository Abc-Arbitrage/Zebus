using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Abc.Zebus.Scan;
using Abc.Zebus.Transport;
using Abc.Zebus.Util.Extensions;
using log4net;

namespace Abc.Zebus.Core
{
    public class BusMessageLogger
    {
        private static readonly ConcurrentDictionary<Type, MessageTypeLogHelper> _logHelpers = new ConcurrentDictionary<Type, MessageTypeLogHelper>();
        private readonly ILog _logger;
        private bool _logDebugEnabled;
        private bool _logInfoEnabled;

        public BusMessageLogger(Type loggerType)
            : this(loggerType.FullName)
        {
        }

        public BusMessageLogger(string loggerFullName)
        {
            _logger = LogManager.GetLogger(typeof(BusMessageLogger).Assembly, loggerFullName);

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
            => _logInfoEnabled && GetLogHelper(message).Logger.IsInfoEnabled;

        public void LogHandleMessage(IList<IMessage> messages, string? dispatchQueueName, MessageId? messageId)
        {
            var message = messages[0];

            if (!TryGetLogHelperForInfo(message, out var logHelper))
                return;

            var messageText = logHelper.GetMessageText(message);
            var dispatchQueueNameText = HasCustomDispatchQueue() ? $" [{dispatchQueueName}]" : "";
            var batchText = messages.Count > 1 ? $" Count: {messages.Count}" : "";

            _logger.Info($"HANDLE{dispatchQueueNameText}: {messageText}{batchText} [{messageId}]");

            bool HasCustomDispatchQueue() => !string.IsNullOrEmpty(dispatchQueueName) && dispatchQueueName != DispatchQueueNameScanner.DefaultQueueName;
        }

        public void LogReceiveMessageAck(MessageExecutionCompleted messageAck)
        {
            if (!TryGetLogHelperForDebug(messageAck, out _))
                return;

            _logger.Debug($"RECV ACK {{{messageAck}}}");
        }

        public void LogReceiveMessageLocal(IMessage message)
        {
            if (!TryGetLogHelperForDebug(message, out var logHelper))
                return;

            var messageText = logHelper.GetMessageText(message);
            _logger.Debug($"RECV local: {messageText}");
        }

        public void LogReceiveMessageRemote(IMessage message, TransportMessage transportMessage)
        {
            if (!TryGetLogHelperForDebug(message, out var logHelper))
                return;

            var messageText = logHelper.GetMessageText(message);
            _logger.Debug($"RECV remote: {messageText} from {transportMessage.SenderId} ({transportMessage.Content?.Length} bytes). [{transportMessage.Id}]");
        }

        public void LogSendMessage(IMessage message, IList<Peer> peers)
        {
            if (!TryGetLogHelperForInfo(message, out var logHelper))
                return;

            var messageText = logHelper.GetMessageText(message);
            var targetPeersText = GetTargetPeersText(peers);

            _logger.Info($"SEND: {messageText} to {targetPeersText}");
        }

        public void LogSendMessage(IMessage message, IList<Peer> peers, TransportMessage transportMessage)
        {
            if (!TryGetLogHelperForInfo(message, out var logHelper))
                return;

            var messageText = logHelper.GetMessageText(message);
            var targetPeersText = GetTargetPeersText(peers);

            _logger.Info($"SEND: {messageText} to {targetPeersText} ({transportMessage.Content?.Length} bytes) [{transportMessage.Id}]");
        }

        private static string GetTargetPeersText(IList<Peer> peers)
        {
            switch (peers.Count)
            {
                case 0:
                    return "no target peer";

                case 1:
                    return peers[0].Id.ToString();

                default:
                    var otherPeersCount = peers.Count - 1;
                    return otherPeersCount > 1
                        ? peers[0].Id + " and " + otherPeersCount + " other peers"
                        : peers[0].Id + " and " + otherPeersCount + " other peer";
            }
        }

        public static string ToString(IMessage message)
            => GetLogHelper(message).GetMessageText(message);

        [SuppressMessage("ReSharper", "ConvertClosureToMethodGroup")]
        private static MessageTypeLogHelper GetLogHelper(IMessage message)
            => _logHelpers.GetOrAdd(message.GetType(), type => CreateLogger(type));

        private static MessageTypeLogHelper CreateLogger(Type messageType)
        {
            var logger = LogManager.GetLogger(messageType);
            var hasToStringOverride = HasToStringOverride(messageType);

            return new MessageTypeLogHelper(logger, hasToStringOverride, messageType.GetPrettyName());
        }

        private bool TryGetLogHelperForInfo(IMessage message, [NotNullWhen(true)] out MessageTypeLogHelper? logHelper)
        {
            if (!_logInfoEnabled)
            {
                logHelper = null;
                return false;
            }

            logHelper = GetLogHelper(message);
            return logHelper.Logger.IsInfoEnabled;
        }

        private bool TryGetLogHelperForDebug(IMessage message, [NotNullWhen(true)] out MessageTypeLogHelper? logHelper)
        {
            if (!_logDebugEnabled)
            {
                logHelper = null;
                return false;
            }

            logHelper = GetLogHelper(message);
            return logHelper.Logger.IsDebugEnabled;
        }

        private static bool HasToStringOverride(Type messageType)
        {
            var methodInfo = messageType.GetMethod("ToString", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            return methodInfo != null;
        }

        private class MessageTypeLogHelper
        {
            public readonly ILog Logger;
            private readonly bool _hasToStringOverride;
            private readonly string _messageTypeName;

            public MessageTypeLogHelper(ILog logger, bool hasToStringOverride, string messageTypeName)
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
