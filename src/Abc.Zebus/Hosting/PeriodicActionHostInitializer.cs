﻿using System;
using System.Threading;
using Abc.Zebus.Lotus;
using Abc.Zebus.Util;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Abc.Zebus.Hosting;

[UsedImplicitly]
public abstract class PeriodicActionHostInitializer : HostInitializer
{
    protected readonly ILogger _logger;
    private readonly IBus _bus;
    private readonly Func<DateTime>? _dueTimeUtcFunc;
    private Timer? _timer;
    private int _exceptionCount;
    private DateTime _nextInvocationUtc;
    private DateTime _pauseEndTimeUtc;

    /// <param name="dueTimeUtcFunc">If this is not provided the UTC due time will be DateTime.UtcNow at the time the initializer is started</param>
    protected PeriodicActionHostInitializer(IBus bus, TimeSpan period, Func<DateTime>? dueTimeUtcFunc = null)
    {
        _logger = ZebusLogManager.GetLogger(GetType());
        _bus = bus;
        _dueTimeUtcFunc = dueTimeUtcFunc;

        Period = period;
    }

    public bool IsRunning => _timer != null;

    /// <summary> This period is applied to the UTC due time calculated by <see cref="_dueTimeUtcFunc"/> </summary>
    public TimeSpan Period { get; }
    public bool ErrorPublicationEnabled { get; set; } = true;
    public int ErrorCountBeforePause { get; set; } = 10;
    public TimeSpan ErrorPauseDuration { get; set; } = 2.Minutes();
    public MissedTimeoutCatchupMode CatchupMode { get; set; } = MissedTimeoutCatchupMode.RunActionForMissedTimeouts;

    public abstract void DoPeriodicAction();

    protected virtual bool ShouldPublishError(Exception error) => ErrorPublicationEnabled;

    public override void AfterStart()
    {
        base.AfterStart();

        if (Period <= TimeSpan.Zero || Period == TimeSpan.MaxValue)
        {
            _logger.LogInformation("Periodic action disabled");
            return;
        }

        _timer = new Timer(_ => OnTimer());
        _nextInvocationUtc = GetInitialInvocationUtc();
        _pauseEndTimeUtc = DateTime.MinValue;

        SetTimer();
    }

    private DateTime GetInitialInvocationUtc()
    {
        var utcNow = DateTime.UtcNow;

        if (_dueTimeUtcFunc == null)
            return utcNow.Add(Period);

        var dueTimeUtc = _dueTimeUtcFunc.Invoke();

        while (dueTimeUtc < utcNow)
            dueTimeUtc = dueTimeUtc.Add(Period);

        return dueTimeUtc;
    }

    private void SetTimer()
    {
        var utcNow = DateTime.UtcNow;
        var dueTime = _nextInvocationUtc > utcNow ? _nextInvocationUtc - utcNow : TimeSpan.Zero;

        _timer?.Change(dueTime, Timeout.InfiniteTimeSpan);
    }

    public override void BeforeStop()
    {
        var timer = _timer;
        if (timer == null)
            return;

        _timer = null;

        var timerStopWaitHandle = new ManualResetEvent(false);
        if (!timer.Dispose(timerStopWaitHandle) || !timerStopWaitHandle.WaitOne(2.Seconds()))
            _logger.LogWarning("Unable to terminate periodic action");
    }

    private void OnTimer()
    {
        InvokePeriodicAction();

        _nextInvocationUtc = _nextInvocationUtc.Add(Period);

        var utcNow = DateTime.UtcNow;

        while (_nextInvocationUtc < utcNow)
        {
            if (CatchupMode == MissedTimeoutCatchupMode.RunActionForMissedTimeouts)
                InvokePeriodicAction();

            _nextInvocationUtc = _nextInvocationUtc.Add(Period);
        }

        SetTimer();
    }

    private void InvokePeriodicAction()
    {
        if (_pauseEndTimeUtc > DateTime.UtcNow)
            return;

        try
        {
            DoPeriodicAction();
            _exceptionCount = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in periodic action");

            if (ShouldPublishError(ex))
                PublishError(ex);

            _exceptionCount++;
            if (_exceptionCount >= ErrorCountBeforePause)
            {
                _logger.LogWarning($"Too many exceptions, periodic action paused ({ErrorPauseDuration})");
                _pauseEndTimeUtc = DateTime.UtcNow.Add(ErrorPauseDuration);
            }
        }
    }

    private void PublishError(Exception error)
    {
        try
        {
            _bus.Publish(new CustomProcessingFailed(GetType().FullName!, error.ToString()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to send CustomProcessingFailed");
        }
    }

    public enum MissedTimeoutCatchupMode
    {
        RunActionForMissedTimeouts,
        SkipMissedTimeouts,
    }
}
