using System;
using Avalonia;

namespace FolderStyleEditorForWindows.Services;

public sealed class SnapSpringAnimator
{
    private readonly double _dampingRatio;
    private readonly double _angularFrequency;
    private readonly double _stopErrorPx;
    private readonly double _stopVelocityPxPerSecond;
    private readonly double _maxStepSeconds;
    private Vector _position;
    private Vector _velocity;
    private Vector _baseTarget;
    private bool _isInitialized;
    private bool _isActive;

    public SnapSpringAnimator(
        double dampingRatio = 0.78,
        double angularFrequency = 20.0,
        double stopErrorPx = 0.25,
        double stopVelocityPxPerSecond = 3.0,
        double maxStepSeconds = 1d / 120d)
    {
        _dampingRatio = dampingRatio;
        _angularFrequency = angularFrequency;
        _stopErrorPx = stopErrorPx;
        _stopVelocityPxPerSecond = stopVelocityPxPerSecond;
        _maxStepSeconds = maxStepSeconds;
    }

    public Vector Position => _position;

    public Vector BaseTarget => _baseTarget;

    public bool IsActive => _isActive;

    public bool IsInitialized => _isInitialized;

    public void Reset(Vector position)
    {
        _position = position;
        _velocity = default;
        _baseTarget = position;
        _isInitialized = true;
        _isActive = false;
    }

    public void Start(Vector position, Vector target)
    {
        _position = position;
        _velocity = default;
        _baseTarget = target;
        _isInitialized = true;
        _isActive = true;
    }

    public void ShiftBaseTarget(Vector target)
    {
        if (!_isInitialized)
        {
            Reset(target);
            return;
        }

        var delta = target - _baseTarget;
        _baseTarget = target;
        _position += delta;
    }

    public void SnapToTarget(Vector target)
    {
        _position = target;
        _velocity = default;
        _baseTarget = target;
        _isInitialized = true;
        _isActive = false;
    }

    public Vector Tick(double deltaSeconds)
    {
        if (!_isInitialized)
        {
            return default;
        }

        if (!_isActive)
        {
            return _position;
        }

        var remaining = Math.Max(0d, deltaSeconds);
        if (remaining <= 0d)
        {
            return _position;
        }

        while (remaining > 0d)
        {
            var step = Math.Min(remaining, _maxStepSeconds);
            var error = _position - _baseTarget;
            var acceleration =
                (-2d * _dampingRatio * _angularFrequency * _velocity) -
                ((_angularFrequency * _angularFrequency) * error);

            _velocity += acceleration * step;
            _position += _velocity * step;
            remaining -= step;
        }

        if ((_position - _baseTarget).Length <= _stopErrorPx &&
            _velocity.Length <= _stopVelocityPxPerSecond)
        {
            _position = _baseTarget;
            _velocity = default;
            _isActive = false;
        }

        return _position;
    }

    public void EnsureActive()
    {
        if (_isInitialized && (_position - _baseTarget).Length > _stopErrorPx)
        {
            _isActive = true;
        }
    }
}
