using System;

namespace Abc.Zebus.Util
{
    internal static class SystemDateTime
    {
        public static DateTime Now 
        { 
            get { return _nowFunc(); }
        }

        public static DateTime UtcNow
        {
            get { return _utcNowFunc(); }
        }

        public static DateTime Today
        {
            get { return _nowFunc().Date; }
        }

        private static Func<DateTime> _nowFunc = () => DateTime.Now;
        private static Func<DateTime> _utcNowFunc = () => DateTime.UtcNow;

        public static void Reset()
        {
            _nowFunc = () => DateTime.Now;
            _utcNowFunc = () => DateTime.UtcNow;
        }

        public static IDisposable Set(DateTime? now = null, DateTime? utcNow = null)
        {
            if (now == null && utcNow == null)
                throw new ArgumentNullException();

            _nowFunc = () => now != null ? now.Value : utcNow.Value.ToLocalTime();
            _utcNowFunc = () => utcNow != null ? utcNow.Value : now.Value.ToUniversalTime();
            return new Scope();
        }

        public static IDisposable PauseTime()
        {
            var now = DateTime.Now;
            return Set(now, now.ToUniversalTime());
        }

        private class Scope : IDisposable
        {
            public void Dispose()
            {
                Reset();
            }
        }
    }
}