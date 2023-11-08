using System;

namespace Abc.Zebus.Util;

internal static class SystemDateTime
{
    public static DateTime UtcNow => _pausedValue ?? DateTime.UtcNow;

    private static DateTime? _pausedValue;

    public static void Reset()
    {
        _pausedValue = null;
    }

    public static IDisposable PauseTime()
    {
        return PauseTime(DateTime.UtcNow);
    }

    public static IDisposable PauseTime(DateTime utcNow)
    {
        _pausedValue = utcNow;

        return new Scope();
    }

    public static void AddToPausedTime(TimeSpan timeSpan)
    {
        if (_pausedValue == null)
            throw new InvalidOperationException($"{nameof(SystemDateTime)} is not paused");

        _pausedValue = _pausedValue.Value.Add(timeSpan);
    }

    private class Scope : IDisposable
    {
        public void Dispose()
        {
            Reset();
        }
    }
}
