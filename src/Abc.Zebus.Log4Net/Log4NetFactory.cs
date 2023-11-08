using System;
using log4net;
using Microsoft.Extensions.Logging;

namespace Abc.Zebus.Log4Net;

public sealed class Log4NetFactory : ILoggerFactory
{
    public void Dispose()
    {
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new Logger(LogManager.GetLogger(categoryName));
    }

    public void AddProvider(ILoggerProvider provider)
    {
    }

    private class Logger : ILogger
    {
        private readonly ILog _log;

        public Logger(ILog log)
        {
            _log = log;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            switch (logLevel)
            {
                case LogLevel.Debug:
                    if (_log.IsDebugEnabled)
                        _log.Debug(formatter(state, exception), exception);
                    break;

                case LogLevel.Information:
                    if (_log.IsInfoEnabled)
                        _log.Info(formatter(state, exception), exception);
                    break;

                case LogLevel.Warning:
                    if (_log.IsWarnEnabled)
                        _log.Warn(formatter(state, exception), exception);
                    break;

                case LogLevel.Error:
                    if (_log.IsErrorEnabled)
                        _log.Error(formatter(state, exception), exception);
                    break;

                case LogLevel.Critical:
                    if (_log.IsFatalEnabled)
                        _log.Fatal(formatter(state, exception), exception);
                    break;
            }
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Debug       => _log.IsDebugEnabled,
                LogLevel.Information => _log.IsInfoEnabled,
                LogLevel.Warning     => _log.IsWarnEnabled,
                LogLevel.Error       => _log.IsErrorEnabled,
                LogLevel.Critical    => _log.IsFatalEnabled,
                _                    => false
            };
        }

        public IDisposable BeginScope<TState>(TState state)
            => EmptyDisposable.Instance;
    }

    private class EmptyDisposable : IDisposable
    {
        public static EmptyDisposable Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
