using System;
using System.Threading;
using Abc.Zebus.Lotus;
using Abc.Zebus.Util;
using Abc.Zebus.Util.Annotations;
using log4net;

namespace Abc.Zebus.Hosting
{
    [UsedImplicitly]
    public abstract class PeriodicActionHostInitializer : HostInitializer
    {
        protected readonly ILog _logger;
        private readonly IBus _bus;
        private TimeSpan _period;
        private Thread _thread;
        private int _exceptionCount;
        private volatile bool _isRunning;
        private readonly Func<DateTime> _startOffset;

        protected PeriodicActionHostInitializer(IBus bus, TimeSpan period, Func<DateTime> startOffset = null)
        {
            _logger = LogManager.GetLogger(GetType());
            _bus = bus;
            _period = period;
            _startOffset = startOffset ?? (() => DateTime.UtcNow);

            ErrorPublicationEnabled = true;
            ErrorCountBeforePause = 10;
            ErrorPauseDuration = 2.Minutes();
        }

        public abstract void DoPeriodicAction();

        public TimeSpan Period { get { return _period; } }
        public bool ErrorPublicationEnabled { get; set; }
        public int ErrorCountBeforePause { get; set; }
        public TimeSpan ErrorPauseDuration { get; set; }

        protected bool IsRunning { get { return _isRunning; } }

        public override void AfterStart()
        {
            base.AfterStart();

            if (Period == TimeSpan.MaxValue)
            {
                _logger.InfoFormat("Periodic action disabled");
                return;
            }

            _isRunning = true;
            _thread = new Thread(MainLoop) { Name = GetType().Name + "MainLoop" };
            _thread.Start();
        }

        public override void BeforeStop()
        {
            if (!_isRunning)
                return;

            _isRunning = false;
            if (!_thread.Join(2000))
                _logger.Warn("Unable to terminate periodic action");
        }

        private void MainLoop()
        {
            var sleep = (int)Math.Min(300, _period.TotalMilliseconds / 2);

            var startTime = _startOffset();
            var startWaitingPeriod = (startTime - DateTime.UtcNow) - _period;
            if (startWaitingPeriod > 5.Minutes())
                throw new InvalidOperationException("Start offset is too large, please review your offset function");

            if (startWaitingPeriod.TotalMilliseconds > 0)
            {
                _logger.InfoFormat("MainLoop sleeping for {0}s before starting", startWaitingPeriod.TotalSeconds);
                Thread.Sleep(startWaitingPeriod);
            }

            _logger.InfoFormat("MainLoop started, Period: {0}ms, Sleep: {1}ms", _period.TotalMilliseconds, sleep);
            var next = DateTime.UtcNow + _period;
            while (_isRunning)
            {
                if (DateTime.UtcNow >= next)
                    InvokePeriodicAction(ref next);
                else
                    Thread.Sleep(sleep);
            }
            _logger.Info("MainLoop stopped");
        }

        private void InvokePeriodicAction(ref DateTime next)
        {
            try
            {
                DoPeriodicAction();
                _exceptionCount = 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                ++_exceptionCount;

                PublishError(ex);
            }
            if (_exceptionCount >= ErrorCountBeforePause)
            {
                _logger.ErrorFormat("Too many exceptions, periodic action paused ({0})", ErrorPauseDuration);
                next = next + _period + ErrorPauseDuration;
            }
            else
            {
                next = next + _period;
            }
        }

        private void PublishError(Exception error)
        {
            if (!ErrorPublicationEnabled)
                return;

            try
            {
                _bus.Publish(new CustomProcessingFailed(GetType().FullName, error.ToString(), DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                _logger.ErrorFormat("Unable to send CustomProcessingFailed, Exception: {0}", ex);
            }
        }
    }
}