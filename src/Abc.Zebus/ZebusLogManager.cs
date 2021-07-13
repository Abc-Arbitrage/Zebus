using System;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Abc.Zebus
{
    public static class ZebusLogManager
    {
        private static ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;

        public static ILoggerFactory LoggerFactory
        {
            get => _loggerFactory;
            set
            {
                value ??= NullLoggerFactory.Instance;

                if (ReferenceEquals(value, _loggerFactory))
                    return;

                _loggerFactory = value;
                LoggerFactoryChanged?.Invoke();
            }
        }

        public static event Action? LoggerFactoryChanged;

        public static ILogger GetLogger(string name)
            => new ForwardingLogger(name);

        public static ILogger GetLogger(Type type)
            => GetLogger(type.FullName!);

        private class ForwardingLogger : ILogger
        {
            private readonly string _name;
            private ILogger _logger;
            private ILoggerFactory _currentLoggerFactory = NullLoggerFactory.Instance;

            private ILogger Logger
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ReferenceEquals(_currentLoggerFactory, _loggerFactory)
                    ? _logger
                    : CreateLogger();
            }

            public ForwardingLogger(string name)
            {
                _name = name;
                _logger = CreateLogger();
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
                => Logger.Log(logLevel, eventId, state, exception, formatter);

            public bool IsEnabled(LogLevel logLevel)
                => Logger.IsEnabled(logLevel);

            public IDisposable BeginScope<TState>(TState state)
                => Logger.BeginScope(state);

            [MethodImpl(MethodImplOptions.NoInlining)]
            private ILogger CreateLogger()
            {
                _logger = _loggerFactory.CreateLogger(_name);
                _currentLoggerFactory = _loggerFactory;
                return _logger;
            }
        }
    }
}
