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
            => new Logger(name);

        public static ILogger GetLogger(Type type)
            => GetLogger(type.FullName!);

        private class Logger : ILogger
        {
            private readonly string _name;
            private ILogger _logger = NullLogger.Instance;
            private ILoggerFactory _currentLoggerFactory = NullLoggerFactory.Instance;

            public Logger(string name)
                => _name = name;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
                => GetLogger().Log(logLevel, eventId, state, exception, formatter);

            public bool IsEnabled(LogLevel logLevel)
                => GetLogger().IsEnabled(logLevel);

            public IDisposable BeginScope<TState>(TState state)
                => GetLogger().BeginScope(state);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private ILogger GetLogger()
            {
                return ReferenceEquals(_currentLoggerFactory, _loggerFactory)
                    ? _logger
                    : GetLoggerSlow();

                [MethodImpl(MethodImplOptions.NoInlining)]
                ILogger GetLoggerSlow()
                {
                    _logger = _loggerFactory.CreateLogger(_name);
                    _currentLoggerFactory = _loggerFactory;
                    return _logger;
                }
            }
        }
    }
}
