using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Linq;
using FolderStyleEditorForWindows.Services;

namespace FolderStyleEditorForWindows.Behaviors
{
    public static class HoverFloatBehavior
    {
        public static readonly AttachedProperty<bool> IsEnabledProperty =
            AvaloniaProperty.RegisterAttached<Control, Control, bool>("IsEnabled");

        public static readonly AttachedProperty<double> HoverScaleProperty =
            AvaloniaProperty.RegisterAttached<Control, Control, double>("HoverScale", 1.08);

        public static readonly AttachedProperty<double> PressScaleProperty =
            AvaloniaProperty.RegisterAttached<Control, Control, double>("PressScale", 0.78);

        public static readonly AttachedProperty<double> MaxOffsetXProperty =
            AvaloniaProperty.RegisterAttached<Control, Control, double>("MaxOffsetX", 2.6);

        public static readonly AttachedProperty<double> MaxOffsetYProperty =
            AvaloniaProperty.RegisterAttached<Control, Control, double>("MaxOffsetY", 2.6);

        public static readonly AttachedProperty<double> LerpFactorProperty =
            AvaloniaProperty.RegisterAttached<Control, Control, double>("LerpFactor", 0.22);

        public static readonly AttachedProperty<double> RenderOriginXProperty =
            AvaloniaProperty.RegisterAttached<Control, Control, double>("RenderOriginX", 0.5);

        public static readonly AttachedProperty<double> RenderOriginYProperty =
            AvaloniaProperty.RegisterAttached<Control, Control, double>("RenderOriginY", 0.5);

        private static readonly Dictionary<Control, HoverFloatState> States = new();

        private static AnimationStateSource? AnimationStateSource =>
            App.Services?.GetService(typeof(AnimationStateSource)) as AnimationStateSource;

        static HoverFloatBehavior()
        {
            IsEnabledProperty.Changed.AddClassHandler<Control>(OnIsEnabledChanged);
        }

        public static bool GetIsEnabled(Control control) => control.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(Control control, bool value) => control.SetValue(IsEnabledProperty, value);

        public static double GetHoverScale(Control control) => control.GetValue(HoverScaleProperty);
        public static void SetHoverScale(Control control, double value) => control.SetValue(HoverScaleProperty, value);

        public static double GetPressScale(Control control) => control.GetValue(PressScaleProperty);
        public static void SetPressScale(Control control, double value) => control.SetValue(PressScaleProperty, value);

        public static double GetMaxOffsetX(Control control) => control.GetValue(MaxOffsetXProperty);
        public static void SetMaxOffsetX(Control control, double value) => control.SetValue(MaxOffsetXProperty, value);

        public static double GetMaxOffsetY(Control control) => control.GetValue(MaxOffsetYProperty);
        public static void SetMaxOffsetY(Control control, double value) => control.SetValue(MaxOffsetYProperty, value);

        public static double GetLerpFactor(Control control) => control.GetValue(LerpFactorProperty);
        public static void SetLerpFactor(Control control, double value) => control.SetValue(LerpFactorProperty, value);

        public static double GetRenderOriginX(Control control) => control.GetValue(RenderOriginXProperty);
        public static void SetRenderOriginX(Control control, double value) => control.SetValue(RenderOriginXProperty, value);

        public static double GetRenderOriginY(Control control) => control.GetValue(RenderOriginYProperty);
        public static void SetRenderOriginY(Control control, double value) => control.SetValue(RenderOriginYProperty, value);

        private static void OnIsEnabledChanged(Control control, AvaloniaPropertyChangedEventArgs args)
        {
            if (args.NewValue is true)
            {
                Attach(control);
                return;
            }

            Detach(control);
        }

        private static void Attach(Control control)
        {
            if (States.ContainsKey(control))
            {
                return;
            }

            var state = new HoverFloatState(control);
            States[control] = state;

            control.PointerEntered += Control_PointerEntered;
            control.PointerMoved += Control_PointerMoved;
            control.PointerExited += Control_PointerExited;
            control.PointerCaptureLost += Control_PointerCaptureLost;
            control.AddHandler(InputElement.PointerPressedEvent, Control_PointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
            control.AddHandler(InputElement.PointerReleasedEvent, Control_PointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
            control.AttachedToVisualTree += Control_AttachedToVisualTree;
            control.DetachedFromVisualTree += Control_DetachedFromVisualTree;
        }

        private static void Detach(Control control)
        {
            if (!States.Remove(control, out var state))
            {
                return;
            }

            control.PointerEntered -= Control_PointerEntered;
            control.PointerMoved -= Control_PointerMoved;
            control.PointerExited -= Control_PointerExited;
            control.PointerCaptureLost -= Control_PointerCaptureLost;
            control.RemoveHandler(InputElement.PointerPressedEvent, Control_PointerPressed);
            control.RemoveHandler(InputElement.PointerReleasedEvent, Control_PointerReleased);
            control.AttachedToVisualTree -= Control_AttachedToVisualTree;
            control.DetachedFromVisualTree -= Control_DetachedFromVisualTree;

            state.Dispose();
        }

        private static void Control_PointerEntered(object? sender, PointerEventArgs e)
        {
            if (sender is not Control control || !States.TryGetValue(control, out var state))
            {
                return;
            }

            AnimationStateSource?.MarkHoverActivity();
            state.SetHovered(true);
            state.UpdatePointer(e.GetPosition(control));
        }

        private static void Control_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (sender is not Control control || !States.TryGetValue(control, out var state))
            {
                return;
            }

            AnimationStateSource?.MarkHoverActivity();
            state.UpdatePointer(e.GetPosition(control));
        }

        private static void Control_PointerExited(object? sender, PointerEventArgs e)
        {
            if (sender is not Control control || !States.TryGetValue(control, out var state))
            {
                return;
            }

            AnimationStateSource?.MarkHoverActivity();
            state.SetHovered(false);
        }

        private static void Control_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Control control || !States.TryGetValue(control, out var state))
            {
                return;
            }

            if (!e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
            {
                return;
            }

            AnimationStateSource?.MarkHoverActivity();
            state.SetPressed(true);
        }

        private static void Control_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (sender is not Control control || !States.TryGetValue(control, out var state))
            {
                return;
            }

            AnimationStateSource?.MarkHoverActivity();
            state.SetPressed(false);
        }

        private static void Control_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            if (sender is Control control && States.TryGetValue(control, out var state))
            {
                AnimationStateSource?.MarkHoverActivity();
                state.SetPressed(false);
            }
        }

        private static void Control_DetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is Control control)
            {
                Detach(control);
            }
        }

        private static void Control_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is Control control && States.TryGetValue(control, out var state))
            {
                state.RefreshTransformTarget();
            }
        }

        private sealed class HoverFloatState : IDisposable
        {
            private readonly Control _control;
            private readonly DispatcherTimer _timer;
            private readonly ScaleTransform _scaleTransform;
            private readonly TranslateTransform _translateTransform;
            private readonly CubicEaseOut _easeOut = new();
            private readonly TemplatedControl? _templatedControl;
            private TransformGroup? _transformGroup;
            private Visual? _transformTarget;
            private ITransform? _originalTransform;
            private RelativePoint _originalRenderTransformOrigin;
            private DateTime _pressedStartedAt = DateTime.MinValue;
            private DateTime _pendingReleaseAt = DateTime.MinValue;
            private double _currentScale = 1;
            private double _targetScale = 1;
            private double _currentX;
            private double _currentY;
            private double _targetX;
            private double _targetY;
            private double _lastNormalizedX;
            private double _lastNormalizedY;
            private double _scaleAnimationFrom = 1;
            private double _scaleAnimationTo = 1;
            private DateTime _scaleAnimationStartedAt = DateTime.UtcNow;
            private TimeSpan _scaleAnimationDuration = TimeSpan.Zero;
            private bool _isScaleAnimating;
            private bool _isHovered;
            private bool _isPressed;
            private bool _releasePending;

            public HoverFloatState(Control control)
            {
                _control = control;
                _scaleTransform = new ScaleTransform(1, 1);
                _translateTransform = new TranslateTransform();

                if (_control is TemplatedControl templatedControl)
                {
                    _templatedControl = templatedControl;
                    _templatedControl.TemplateApplied += TemplatedControl_TemplateApplied;
                    Dispatcher.UIThread.Post(
                        RefreshTransformTarget,
                        DispatcherPriority.Loaded);
                }

                SetTransformTarget(_control);

                _timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(16)
                };
                _timer.Tick += Timer_Tick;
            }

            private void TemplatedControl_TemplateApplied(object? sender, TemplateAppliedEventArgs e)
            {
                var target = e.NameScope.Find<Control>("PART_Border") ?? _control;
                SetTransformTarget(target);
            }

            public void RefreshTransformTarget()
            {
                if (_templatedControl == null)
                {
                    SetTransformTarget(_control);
                    return;
                }

                var target = _templatedControl
                    .GetVisualDescendants()
                    .OfType<Control>()
                    .FirstOrDefault(x => x.Name == "PART_Border");

                SetTransformTarget(target ?? _control);
            }

            private void SetTransformTarget(Visual target)
            {
                if (ReferenceEquals(_transformTarget, target))
                {
                    return;
                }

                RestoreTransformTarget();

                _transformTarget = target;
                _originalTransform = target.RenderTransform;
                _originalRenderTransformOrigin = target.RenderTransformOrigin;

                _transformGroup = new TransformGroup();
                if (_originalTransform is Transform originalTransform)
                {
                    _transformGroup.Children.Add(originalTransform);
                }

                _transformGroup.Children.Add(_scaleTransform);
                _transformGroup.Children.Add(_translateTransform);

                _transformTarget.RenderTransformOrigin = new RelativePoint(
                    Math.Clamp(GetRenderOriginX(_control), 0, 1),
                    Math.Clamp(GetRenderOriginY(_control), 0, 1),
                    RelativeUnit.Relative);
                _transformTarget.RenderTransform = _transformGroup;

                ApplyCurrentTransform();
            }

            private void RestoreTransformTarget()
            {
                if (_transformTarget == null)
                {
                    return;
                }

                _transformTarget.RenderTransform = _originalTransform;
                _transformTarget.RenderTransformOrigin = _originalRenderTransformOrigin;
                _transformTarget = null;
                _originalTransform = null;
                _transformGroup = null;
            }

            public void SetHovered(bool isHovered)
            {
                _isHovered = isHovered;
                UpdateTargetScale();

                if (!_isHovered)
                {
                    _targetX = 0;
                    _targetY = 0;
                }

                EnsureTimer();
            }

            public void SetPressed(bool isPressed)
            {
                if (!isPressed && _isPressed)
                {
                    var minimumPressedDuration = TimeSpan.FromMilliseconds(95);
                    var elapsed = DateTime.UtcNow - _pressedStartedAt;
                    if (elapsed < minimumPressedDuration)
                    {
                        _releasePending = true;
                        _pendingReleaseAt = _pressedStartedAt + minimumPressedDuration;
                        EnsureTimer();
                        return;
                    }
                }

                _releasePending = false;
                _isPressed = isPressed;
                if (isPressed)
                {
                    _pressedStartedAt = DateTime.UtcNow;
                }

                UpdateTargetScale();
                EnsureTimer();
            }

            public void UpdatePointer(Point point)
            {
                if (!_isHovered)
                {
                    return;
                }

                var bounds = _control.Bounds;
                if (bounds.Width <= 0 || bounds.Height <= 0)
                {
                    return;
                }

                var normalizedX = Math.Clamp(((point.X / bounds.Width) - 0.5) * 2.0, -1.0, 1.0);
                var normalizedY = Math.Clamp(((point.Y / bounds.Height) - 0.5) * 2.0, -1.0, 1.0);

                _lastNormalizedX = normalizedX;
                _lastNormalizedY = normalizedY;

                if (_isPressed)
                {
                    return;
                }

                _targetX = normalizedX * GetMaxOffsetX(_control);
                _targetY = normalizedY * GetMaxOffsetY(_control);
                EnsureTimer();
            }

            private void EnsureTimer()
            {
                if (!_timer.IsEnabled)
                {
                    _timer.Start();
                }
            }

            private void UpdateTargetScale()
            {
                if (_isPressed)
                {
                    _targetScale = GetPressScale(_control);
                    _targetX = 0;
                    _targetY = 0;
                    StartScaleAnimation(TimeSpan.FromMilliseconds(70));
                    return;
                }

                _targetScale = _isHovered ? GetHoverScale(_control) : 1;
                _targetX = _isHovered ? _lastNormalizedX * GetMaxOffsetX(_control) : 0;
                _targetY = _isHovered ? _lastNormalizedY * GetMaxOffsetY(_control) : 0;
                StartScaleAnimation(TimeSpan.FromMilliseconds(168));
            }

            private void StartScaleAnimation(TimeSpan duration)
            {
                if (Math.Abs(_currentScale - _targetScale) < 0.0005)
                {
                    _currentScale = _targetScale;
                    _isScaleAnimating = false;
                    return;
                }

                _scaleAnimationFrom = _currentScale;
                _scaleAnimationTo = _targetScale;
                _scaleAnimationStartedAt = DateTime.UtcNow;
                _scaleAnimationDuration = duration;
                _isScaleAnimating = true;
            }

            private void Timer_Tick(object? sender, EventArgs e)
            {
                if (_control.GetVisualRoot() == null || !_control.IsEffectivelyVisible)
                {
                    ResetToIdentity();
                    _timer.Stop();
                    return;
                }

                if (_releasePending && DateTime.UtcNow >= _pendingReleaseAt)
                {
                    _releasePending = false;
                    _isPressed = false;
                    UpdateTargetScale();
                }

                var lerpFactor = Math.Clamp(GetLerpFactor(_control), 0.05, 0.45);
                if (_isPressed)
                {
                    lerpFactor = Math.Max(lerpFactor, 0.34);
                }

                _currentX = Lerp(_currentX, _targetX, lerpFactor);
                _currentY = Lerp(_currentY, _targetY, lerpFactor);

                if (_isScaleAnimating)
                {
                    var elapsed = DateTime.UtcNow - _scaleAnimationStartedAt;
                    if (elapsed >= _scaleAnimationDuration)
                    {
                        _currentScale = _scaleAnimationTo;
                        _isScaleAnimating = false;
                    }
                    else
                    {
                        var progress = elapsed.TotalMilliseconds / _scaleAnimationDuration.TotalMilliseconds;
                        var easedProgress = _easeOut.Ease(Math.Clamp(progress, 0, 1));
                        _currentScale = _scaleAnimationFrom + ((_scaleAnimationTo - _scaleAnimationFrom) * easedProgress);
                    }
                }
                else
                {
                    _currentScale = _targetScale;
                }

                ApplyCurrentTransform();

                var settled = Math.Abs(_currentScale - _targetScale) < 0.001 &&
                              Math.Abs(_currentX - _targetX) < 0.01 &&
                              Math.Abs(_currentY - _targetY) < 0.01;

                if (_releasePending || !settled)
                {
                    return;
                }

                _isScaleAnimating = false;

                if (!_isHovered && !_isPressed)
                {
                    ResetToIdentity();
                }

                _timer.Stop();
            }

            private void ResetToIdentity()
            {
                _releasePending = false;
                _isPressed = false;
                _isHovered = false;
                _targetScale = 1;
                _targetX = 0;
                _targetY = 0;
                _scaleTransform.ScaleX = 1;
                _scaleTransform.ScaleY = 1;
                _translateTransform.X = 0;
                _translateTransform.Y = 0;
                _currentScale = 1;
                _currentX = 0;
                _currentY = 0;
            }

            public void Dispose()
            {
                _timer.Stop();
                _timer.Tick -= Timer_Tick;
                if (_templatedControl != null)
                {
                    _templatedControl.TemplateApplied -= TemplatedControl_TemplateApplied;
                }

                RestoreTransformTarget();
            }

            private static double Lerp(double from, double to, double factor)
            {
                return from + ((to - from) * factor);
            }

            private void ApplyCurrentTransform()
            {
                _scaleTransform.ScaleX = _currentScale;
                _scaleTransform.ScaleY = _currentScale;
                _translateTransform.X = _currentX;
                _translateTransform.Y = _currentY;
                _transformTarget?.InvalidateVisual();
                _control.InvalidateVisual();
            }
        }
    }
}
