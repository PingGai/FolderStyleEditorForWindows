using System;
using System.Diagnostics;
using Avalonia.Threading;

namespace FolderStyleEditorForWindows.Services;

public readonly record struct MotionProfile(
    TimeSpan Duration,
    Func<double, double> Easing,
    double OvershootRatio = 0d,
    double SettleStart = 1d)
{
    public static MotionProfile Smooth(TimeSpan duration)
        => new(duration, MotionEasings.CubicOut);

    public static MotionProfile SoftSettle(TimeSpan duration, double overshootRatio = 0.035d, double settleStart = 0.82d)
        => new(duration, MotionEasings.CubicOut, overshootRatio, settleStart);
}

public static class MotionEasings
{
    public static double CubicOut(double progress)
    {
        progress = Math.Clamp(progress, 0d, 1d);
        var oneMinus = 1d - progress;
        return 1d - (oneMinus * oneMinus * oneMinus);
    }
}

public sealed class InterruptibleScalarAnimator : IDisposable
{
    private const double ValueEpsilon = 0.0001d;
    private readonly Func<double> _readCurrent;
    private readonly Action<double> _apply;
    private readonly DispatcherTimer _timer;
    private MotionProfile _profile;
    private long _startedTimestamp;
    private double _startValue;
    private double _targetValue;
    private double _lastAppliedValue;
    private bool _hasLastAppliedValue;
    private bool _isAnimating;

    public InterruptibleScalarAnimator(Func<double> readCurrent, Action<double> apply)
    {
        _readCurrent = readCurrent;
        _apply = apply;
        _timer = new DispatcherTimer(DispatcherPriority.Render);
        SetFrameRate(60);
        _timer.Tick += Timer_Tick;
    }

    public void SetFrameRate(int fps)
    {
        fps = Math.Clamp(fps, 1, 240);
        _timer.Interval = TimeSpan.FromSeconds(1d / fps);
    }

    public void Snap(double value)
    {
        _isAnimating = false;
        _timer.Stop();
        ApplyValueIfChanged(value);
    }

    public void AnimateTo(double targetValue, MotionProfile profile)
    {
        if (_isAnimating)
        {
            if (Math.Abs(_targetValue - targetValue) <= ValueEpsilon && _profile.Equals(profile))
            {
                return;
            }

            ApplyCurrentFrame();
        }

        var current = _readCurrent();
        if (Math.Abs(current - targetValue) <= ValueEpsilon)
        {
            Snap(targetValue);
            return;
        }

        _startValue = current;
        _targetValue = targetValue;
        _profile = profile;
        _startedTimestamp = Stopwatch.GetTimestamp();
        _isAnimating = true;
        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
        ApplyCurrentFrame();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (!_isAnimating)
        {
            _timer.Stop();
            return;
        }

        ApplyCurrentFrame();

        if (GetElapsed() >= _profile.Duration)
        {
            Snap(_targetValue);
        }
    }

    private void ApplyCurrentFrame()
    {
        var progress = _profile.Duration <= TimeSpan.Zero
            ? 1d
            : Math.Clamp(GetElapsed().TotalMilliseconds / _profile.Duration.TotalMilliseconds, 0d, 1d);
        ApplyValueIfChanged(Evaluate(progress));
    }

    private TimeSpan GetElapsed()
    {
        var elapsedTicks = Stopwatch.GetTimestamp() - _startedTimestamp;
        var seconds = elapsedTicks / (double)Stopwatch.Frequency;
        return TimeSpan.FromSeconds(seconds);
    }

    private double Evaluate(double progress)
    {
        if (_profile.OvershootRatio <= 0d || _profile.SettleStart >= 1d)
        {
            return Lerp(_startValue, _targetValue, _profile.Easing(progress));
        }

        var overshootTarget = _targetValue + ((_targetValue - _startValue) * _profile.OvershootRatio);
        if (progress < _profile.SettleStart)
        {
            var local = progress / _profile.SettleStart;
            return Lerp(_startValue, overshootTarget, _profile.Easing(local));
        }

        var settleSpan = Math.Max(0.0001d, 1d - _profile.SettleStart);
        var settleProgress = (progress - _profile.SettleStart) / settleSpan;
        return Lerp(overshootTarget, _targetValue, _profile.Easing(settleProgress));
    }

    private static double Lerp(double from, double to, double progress)
    {
        return from + ((to - from) * progress);
    }

    private void ApplyValueIfChanged(double value)
    {
        if (_hasLastAppliedValue && Math.Abs(_lastAppliedValue - value) <= ValueEpsilon)
        {
            return;
        }

        _lastAppliedValue = value;
        _hasLastAppliedValue = true;
        _apply(value);
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= Timer_Tick;
    }
}
