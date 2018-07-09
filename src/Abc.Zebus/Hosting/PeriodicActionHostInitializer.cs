using System;
using System.Threading;
using Abc.Zebus.Lotus;
using Abc.Zebus.Util;
using JetBrains.Annotations;
using log4net;

namespace Abc.Zebus.Hosting
{
    [UsedImplicitly]
    public abstract class PeriodicActionHostInitializer : HostInitializer
    {
        protected readonly ILog _logger;
        private readonly object _avoidConcurrentExecutionsLock = new object();
        private readonly IBus _bus;
        private readonly TimeSpan _period;
        private readonly Func<DateTime> _startOffset;
        private readonly Timer _timer;

        private volatile bool _isRunning;
        private int _exceptionCount;

        protected PeriodicActionHostInitializer(IBus bus, TimeSpan period, Func<DateTime> startOffset = null)
        {
            _logger = LogManager.GetLogger(GetType());
            _bus = bus;
            _period = period;
            _startOffset = startOffset ?? (() => DateTime.UtcNow);
            _timer = new Timer(OnTimer);

            ErrorPublicationEnabled = true;
            ErrorCountBeforePause = 10;
            ErrorPauseDuration = 2.Minutes();
        }

        public abstract void DoPeriodicAction();

        public TimeSpan Period => _period;
        public bool ErrorPublicationEnabled { get; set; }
        public int ErrorCountBeforePause { get; set; }
        public TimeSpan ErrorPauseDuration { get; set; }

        protected bool IsRunning => _isRunning;

        public override void AfterStart()
        {
            base.AfterStart();

            if (Period == TimeSpan.MaxValue)
            {
                _logger.InfoFormat("Periodic action disabled");
                return;
            }

            _isRunning = true;
            
            var startTime = _startOffset();
            var startWaitingPeriod = (startTime - DateTime.UtcNow);
            if (startWaitingPeriod > 5.Minutes())
                throw new InvalidOperationException("Start offset is too large, please review your offset function");

            if(startWaitingPeriod > TimeSpan.Zero)
                _logger.Info($"Periodic action of type {GetType().FullName} has a start offset specified, the start will be delayed for {startWaitingPeriod.TotalSeconds} seconds");

            _timer.Change(startWaitingPeriod + Period, Period);
        }

        public override void BeforeStop()
        {
            if (!_isRunning)
                return;

            _isRunning = false;
            var timerStopWaitHandle = new ManualResetEvent(false);
            if (!_timer.Dispose(timerStopWaitHandle) || !timerStopWaitHandle.WaitOne(2.Seconds()))
                _logger.Warn("Unable to terminate periodic action");
        }

        private void OnTimer(object state)
        {
            InvokePeriodicAction();
        }

        private void InvokePeriodicAction()
        {
            if (!Monitor.TryEnter(_avoidConcurrentExecutionsLock))
            {
                _logger.WarnFormat("Periodic action taking too much time to execute (at least more than configured period {0})", Period);
                return;
            }

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
            finally
            {
                Monitor.Exit(_avoidConcurrentExecutionsLock);
            }
            if (_exceptionCount < ErrorCountBeforePause)
                return;

            _logger.ErrorFormat("Too many exceptions, periodic action paused ({0})", ErrorPauseDuration);
            _timer.Change(ErrorPauseDuration, Period);
        }

        private void PublishError(Exception error)
        {
            if (!ErrorPublicationEnabled)
                return;

            try
            {
                _bus.Publish(new CustomProcessingFailed(GetType().FullName, error.ToString()));
            }
            catch (Exception ex)
            {
                _logger.ErrorFormat("Unable to send CustomProcessingFailed, Exception: {0}", ex);
            }
        }
    }
}