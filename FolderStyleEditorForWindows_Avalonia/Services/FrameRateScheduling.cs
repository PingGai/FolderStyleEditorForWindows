using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using FolderStyleEditorForWindows.Models;

namespace FolderStyleEditorForWindows.Services
{
    public sealed class FrameRateSettings : INotifyPropertyChanged
    {
        private int _staticContentRefreshFps;
        private int _backgroundAmbientFps = 8;
        private int _homeTitleAmbientFps = 15;
        private int _adminTitleAmbientFps = 15;
        private int _activeInteractionFps = 60;
        private bool _useDisplayRefreshRateAsMaxFps = true;
        private int _manualMaxFps = 120;
        private int _hoverCooldownMs = 120;
        private int _scrollCooldownMs = 240;
        private int _dragCooldownMs = 280;
        private bool _showPerformanceMonitor;
        private bool _showDetailedPerformanceMonitor;
        private bool _showComponentFpsBadges;
        private bool _enableComponentExcludeMode;
        private bool _excludePinGlow;
        private bool _excludeBottomActionButtons;
        private bool _excludeActualTopmost;
        private bool _disableEditScrollAnimations;
        private int _displayRefreshRateHz = 60;
        private int _currentForegroundTargetFps;
        private int _currentAmbientTargetFps = 15;
        private bool _isDirty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int StaticContentRefreshFps { get => _staticContentRefreshFps; set => SetField(ref _staticContentRefreshFps, Clamp(value, 0, 240)); }
        public int BackgroundAmbientFps { get => _backgroundAmbientFps; set => SetField(ref _backgroundAmbientFps, Clamp(value, 1, 120)); }
        public int HomeTitleAmbientFps { get => _homeTitleAmbientFps; set => SetField(ref _homeTitleAmbientFps, Clamp(value, 1, 120)); }
        public int AdminTitleAmbientFps { get => _adminTitleAmbientFps; set => SetField(ref _adminTitleAmbientFps, Clamp(value, 1, 120)); }
        public int ActiveInteractionFps { get => _activeInteractionFps; set => SetField(ref _activeInteractionFps, Clamp(value, 1, 240)); }
        public bool UseDisplayRefreshRateAsMaxFps { get => _useDisplayRefreshRateAsMaxFps; set => SetField(ref _useDisplayRefreshRateAsMaxFps, value); }
        public int ManualMaxFps { get => _manualMaxFps; set => SetField(ref _manualMaxFps, Clamp(value, 1, 500)); }
        public int HoverCooldownMs { get => _hoverCooldownMs; set => SetField(ref _hoverCooldownMs, Clamp(value, 0, 5000)); }
        public int ScrollCooldownMs { get => _scrollCooldownMs; set => SetField(ref _scrollCooldownMs, Clamp(value, 0, 5000)); }
        public int DragCooldownMs { get => _dragCooldownMs; set => SetField(ref _dragCooldownMs, Clamp(value, 0, 5000)); }
        public bool ShowPerformanceMonitor { get => _showPerformanceMonitor; set => SetField(ref _showPerformanceMonitor, value); }
        public bool ShowDetailedPerformanceMonitor { get => _showDetailedPerformanceMonitor; set => SetField(ref _showDetailedPerformanceMonitor, value); }
        public bool ShowComponentFpsBadges { get => _showComponentFpsBadges; set => SetField(ref _showComponentFpsBadges, value); }
        public bool EnableComponentExcludeMode { get => _enableComponentExcludeMode; set => SetField(ref _enableComponentExcludeMode, value); }
        public bool ExcludePinGlow { get => _excludePinGlow; set => SetField(ref _excludePinGlow, value); }
        public bool ExcludeBottomActionButtons { get => _excludeBottomActionButtons; set => SetField(ref _excludeBottomActionButtons, value); }
        public bool ExcludeActualTopmost { get => _excludeActualTopmost; set => SetField(ref _excludeActualTopmost, value); }
        public bool DisableEditScrollAnimations { get => _disableEditScrollAnimations; set => SetField(ref _disableEditScrollAnimations, value); }

        public int DisplayRefreshRateHz { get => _displayRefreshRateHz; set => SetField(ref _displayRefreshRateHz, Clamp(value, 30, 500), false); }
        public int CurrentForegroundTargetFps { get => _currentForegroundTargetFps; set => SetField(ref _currentForegroundTargetFps, Clamp(value, 0, 500), false); }
        public int CurrentAmbientTargetFps { get => _currentAmbientTargetFps; set => SetField(ref _currentAmbientTargetFps, Clamp(value, 0, 500), false); }
        public bool IsDirty { get => _isDirty; set => SetField(ref _isDirty, value, false); }

        public void LoadFromConfig(AppConfig config)
        {
            var cfg = config.FrameRate ?? new FrameRateBehaviorConfig();
            _staticContentRefreshFps = Clamp(cfg.StaticContentRefreshFps, 0, 240);
            _backgroundAmbientFps = Clamp(cfg.BackgroundAmbientFps, 1, 120);
            _homeTitleAmbientFps = Clamp(cfg.HomeTitleAmbientFps, 1, 120);
            _adminTitleAmbientFps = Clamp(cfg.AdminTitleAmbientFps, 1, 120);
            _activeInteractionFps = Clamp(cfg.ActiveInteractionFps, 1, 240);
            _useDisplayRefreshRateAsMaxFps = cfg.UseDisplayRefreshRateAsMaxFps;
            _manualMaxFps = Clamp(cfg.ManualMaxFps, 1, 500);
            _hoverCooldownMs = Clamp(cfg.HoverCooldownMs, 0, 5000);
            _scrollCooldownMs = Clamp(cfg.ScrollCooldownMs, 0, 5000);
            _dragCooldownMs = Clamp(cfg.DragCooldownMs, 0, 5000);
            _showPerformanceMonitor = cfg.ShowPerformanceMonitor || cfg.ShowFrameRateOverlay;
            _showDetailedPerformanceMonitor = cfg.ShowDetailedPerformanceMonitor || cfg.ShowDetailedFrameRateOverlay;
            _showComponentFpsBadges = cfg.ShowComponentFpsBadges;
            _enableComponentExcludeMode = cfg.EnableComponentExcludeMode;
            _excludePinGlow = cfg.ExcludePinGlow;
            _excludeBottomActionButtons = cfg.ExcludeBottomActionButtons;
            _excludeActualTopmost = cfg.ExcludeActualTopmost;
            _disableEditScrollAnimations = cfg.DisableEditScrollAnimations;
            _currentAmbientTargetFps = Math.Max(_backgroundAmbientFps, Math.Max(_homeTitleAmbientFps, _adminTitleAmbientFps));
            _isDirty = false;
            RaiseAll();
        }

        public void ApplyDefaults()
        {
            StaticContentRefreshFps = 0;
            BackgroundAmbientFps = 8;
            HomeTitleAmbientFps = 15;
            AdminTitleAmbientFps = 15;
            ActiveInteractionFps = 60;
            UseDisplayRefreshRateAsMaxFps = true;
            ManualMaxFps = 120;
            HoverCooldownMs = 120;
            ScrollCooldownMs = 240;
            DragCooldownMs = 280;
            ShowPerformanceMonitor = false;
            ShowDetailedPerformanceMonitor = false;
            ShowComponentFpsBadges = false;
            EnableComponentExcludeMode = false;
            ExcludePinGlow = false;
            ExcludeBottomActionButtons = false;
            ExcludeActualTopmost = false;
            DisableEditScrollAnimations = false;
        }

        public FrameRateBehaviorConfig Export()
        {
            return new FrameRateBehaviorConfig
            {
                StaticContentRefreshFps = StaticContentRefreshFps,
                BackgroundAmbientFps = BackgroundAmbientFps,
                HomeTitleAmbientFps = HomeTitleAmbientFps,
                AdminTitleAmbientFps = AdminTitleAmbientFps,
                ActiveInteractionFps = ActiveInteractionFps,
                UseDisplayRefreshRateAsMaxFps = UseDisplayRefreshRateAsMaxFps,
                ManualMaxFps = ManualMaxFps,
                HoverCooldownMs = HoverCooldownMs,
                ScrollCooldownMs = ScrollCooldownMs,
                DragCooldownMs = DragCooldownMs,
                ShowPerformanceMonitor = ShowPerformanceMonitor,
                ShowDetailedPerformanceMonitor = ShowDetailedPerformanceMonitor,
                ShowComponentFpsBadges = ShowComponentFpsBadges,
                EnableComponentExcludeMode = EnableComponentExcludeMode,
                ExcludePinGlow = ExcludePinGlow,
                ExcludeBottomActionButtons = ExcludeBottomActionButtons,
                ExcludeActualTopmost = ExcludeActualTopmost,
                DisableEditScrollAnimations = DisableEditScrollAnimations,
                ShowFrameRateOverlay = ShowPerformanceMonitor,
                ShowDetailedFrameRateOverlay = ShowDetailedPerformanceMonitor
            };
        }

        public void MarkSaved()
        {
            IsDirty = false;
        }

        private bool SetField<T>(ref T field, T value, bool markDirty = true, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            if (markDirty)
            {
                _isDirty = true;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDirty)));
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        private void RaiseAll()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }

        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
    }

    public sealed class DisplayInfoService
    {
        private readonly FrameRateSettings _settings;
        private int _cachedRefreshRateHz = 60;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct DevMode
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DevMode devMode);

        public DisplayInfoService(FrameRateSettings settings)
        {
            _settings = settings;
            Refresh();
        }

        public int CurrentRefreshRateHz => _cachedRefreshRateHz;

        public void Refresh()
        {
            var hz = 60;
            try
            {
                var mode = new DevMode
                {
                    dmSize = (short)Marshal.SizeOf<DevMode>()
                };
                const int currentSettings = -1;
                if (OperatingSystem.IsWindows() && EnumDisplaySettings(null, currentSettings, ref mode) && mode.dmDisplayFrequency > 0)
                {
                    hz = mode.dmDisplayFrequency;
                }
            }
            catch
            {
                hz = 60;
            }

            _cachedRefreshRateHz = Math.Max(30, Math.Min(500, hz));
            _settings.DisplayRefreshRateHz = _cachedRefreshRateHz;
        }
    }

    public readonly struct FrameRateStateSnapshot
    {
        public FrameRateStateSnapshot(bool isDragging, bool isScrolling, bool isHovering, bool isToastAnimating, bool isTransitionAnimating, bool hasStaticDirtyRegion)
        {
            IsDragging = isDragging;
            IsScrolling = isScrolling;
            IsHovering = isHovering;
            IsToastAnimating = isToastAnimating;
            IsTransitionAnimating = isTransitionAnimating;
            HasStaticDirtyRegion = hasStaticDirtyRegion;
        }

        public bool IsDragging { get; }
        public bool IsScrolling { get; }
        public bool IsHovering { get; }
        public bool IsToastAnimating { get; }
        public bool IsTransitionAnimating { get; }
        public bool HasStaticDirtyRegion { get; }
    }

    public sealed class AnimationStateSource
    {
        private readonly FrameRateSettings _settings;
        private bool _isDragging;
        private bool _isToastAnimating;
        private bool _isTransitionAnimating;
        private bool _hasStaticDirtyRegion = true;
        private long _hoverActiveUntilTicks;
        private long _scrollActiveUntilTicks;
        private long _dragCooldownUntilTicks;
        private long _transitionActiveUntilTicks;

        public event EventHandler? StateChanged;

        public AnimationStateSource(FrameRateSettings settings)
        {
            _settings = settings;
        }

        public void MarkHoverActivity()
        {
            _hoverActiveUntilTicks = AddMs(Stopwatch.GetTimestamp(), Math.Max(_settings.HoverCooldownMs, 320));
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void MarkScrollActivity()
        {
            _scrollActiveUntilTicks = AddMs(Stopwatch.GetTimestamp(), _settings.ScrollCooldownMs);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetDragging(bool dragging)
        {
            if (_isDragging == dragging)
            {
                if (!dragging)
                {
                    _dragCooldownUntilTicks = AddMs(Stopwatch.GetTimestamp(), _settings.DragCooldownMs);
                    StateChanged?.Invoke(this, EventArgs.Empty);
                }

                return;
            }

            _isDragging = dragging;
            if (!dragging)
            {
                _dragCooldownUntilTicks = AddMs(Stopwatch.GetTimestamp(), _settings.DragCooldownMs);
            }

            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetToastAnimating(bool animating)
        {
            if (_isToastAnimating == animating)
            {
                return;
            }

            _isToastAnimating = animating;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetTransitionAnimating(bool animating)
        {
            if (_isTransitionAnimating == animating)
            {
                if (!animating)
                {
                    _transitionActiveUntilTicks = 0;
                }

                return;
            }

            _isTransitionAnimating = animating;
            if (!animating)
            {
                _transitionActiveUntilTicks = 0;
            }

            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void MarkTransitionActivity(int durationMs)
        {
            _transitionActiveUntilTicks = AddMs(Stopwatch.GetTimestamp(), Math.Max(0, durationMs));
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void MarkStaticDirty()
        {
            _hasStaticDirtyRegion = true;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ClearStaticDirty()
        {
            _hasStaticDirtyRegion = false;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public FrameRateStateSnapshot Snapshot()
        {
            var now = Stopwatch.GetTimestamp();
            var isHovering = now <= _hoverActiveUntilTicks;
            var isScrolling = now <= _scrollActiveUntilTicks;
            var isDragCooling = now <= _dragCooldownUntilTicks;
            var isTransitionActive = _isTransitionAnimating || now <= _transitionActiveUntilTicks;
            return new FrameRateStateSnapshot(
                _isDragging || isDragCooling,
                isScrolling,
                isHovering,
                _isToastAnimating,
                isTransitionActive,
                _hasStaticDirtyRegion);
        }

        private static long AddMs(long ticks, int ms)
        {
            if (ms <= 0)
            {
                return ticks;
            }

            var delta = (long)(Stopwatch.Frequency * (ms / 1000.0));
            return ticks + delta;
        }
    }

    public sealed class FrameRateGovernor
    {
        private readonly FrameRateSettings _settings;

        public FrameRateGovernor(FrameRateSettings settings)
        {
            _settings = settings;
        }

        public int ForegroundTargetFps { get; private set; }
        public int AmbientTargetFps { get; private set; }

        public void Update(FrameRateStateSnapshot snapshot, int displayRefreshRateHz)
        {
            if (DebugRuntimeAnalysis.PauseAnimations)
            {
                ForegroundTargetFps = 0;
                AmbientTargetFps = 0;
                _settings.CurrentForegroundTargetFps = 0;
                _settings.CurrentAmbientTargetFps = 0;
                return;
            }

            var displayHz = Math.Max(30, Math.Min(500, displayRefreshRateHz));
            var maxFps = _settings.UseDisplayRefreshRateAsMaxFps
                ? displayHz
                : Math.Min(displayHz, _settings.ManualMaxFps);

            if (snapshot.IsDragging || snapshot.IsScrolling)
            {
                ForegroundTargetFps = maxFps;
            }
            else if (snapshot.IsToastAnimating || snapshot.IsTransitionAnimating || snapshot.IsHovering)
            {
                ForegroundTargetFps = Math.Min(displayHz, _settings.ActiveInteractionFps);
            }
            else
            {
                ForegroundTargetFps = 0;
            }

            AmbientTargetFps = Math.Max(_settings.BackgroundAmbientFps, Math.Max(_settings.HomeTitleAmbientFps, _settings.AdminTitleAmbientFps));
            _settings.CurrentForegroundTargetFps = ForegroundTargetFps;
            _settings.CurrentAmbientTargetFps = AmbientTargetFps;
        }
    }

    public readonly struct RenderDecision
    {
        public RenderDecision(bool shouldRenderForeground, bool shouldRenderStatic, TimeSpan? nextWakeDelay)
        {
            ShouldRenderForeground = shouldRenderForeground;
            ShouldRenderStatic = shouldRenderStatic;
            NextWakeDelay = nextWakeDelay;
        }

        public bool ShouldRenderForeground { get; }
        public bool ShouldRenderStatic { get; }
        public TimeSpan? NextWakeDelay { get; }
    }

    public sealed class RenderScheduler
    {
        private long _lastForegroundTicks;
        private long _lastStaticTicks;

        public RenderDecision Evaluate(int foregroundTargetFps, bool hasStaticDirtyRegion, int staticContentRefreshFps)
        {
            var now = Stopwatch.GetTimestamp();
            var shouldRenderForeground = false;
            TimeSpan? nextWakeDelay = null;
            if (foregroundTargetFps > 0)
            {
                var elapsed = now - _lastForegroundTicks;
                var interval = Stopwatch.Frequency / (double)foregroundTargetFps;
                if (_lastForegroundTicks == 0 || elapsed >= interval)
                {
                    _lastForegroundTicks = now;
                    shouldRenderForeground = true;
                }
            }

            var shouldRenderStatic = false;
            if (hasStaticDirtyRegion)
            {
                if (staticContentRefreshFps <= 0)
                {
                    shouldRenderStatic = true;
                    _lastStaticTicks = now;
                }
                else
                {
                    var elapsed = now - _lastStaticTicks;
                    var interval = Stopwatch.Frequency / (double)staticContentRefreshFps;
                    if (_lastStaticTicks == 0 || elapsed >= interval)
                    {
                        _lastStaticTicks = now;
                        shouldRenderStatic = true;
                    }
                    else
                    {
                        var remainingTicks = Math.Max(1, interval - elapsed);
                        nextWakeDelay = TimeSpan.FromSeconds(remainingTicks / (double)Stopwatch.Frequency);
                    }
                }
            }

            return new RenderDecision(shouldRenderForeground, shouldRenderStatic, nextWakeDelay);
        }
    }

    public enum RenderLayer
    {
        Background,
        Ambient,
        Content,
        Overlay,
        Static
    }

    public sealed class LayerInvalidationController
    {
        private readonly Dictionary<RenderLayer, Action> _layerInvalidators = new();

        public void Bind(RenderLayer layer, Action invalidator)
        {
            _layerInvalidators[layer] = invalidator;
        }

        public void Invalidate(RenderLayer layer)
        {
            if (_layerInvalidators.TryGetValue(layer, out var action))
            {
                action();
            }
        }
    }

    public interface IAmbientAnimationHandle : IDisposable
    {
        void SetEnabled(bool enabled);
    }

    public sealed class AmbientAnimationScheduler
    {
        private readonly PerformanceTelemetryService _telemetryService;

        private sealed class Channel
        {
            public string Id { get; init; } = string.Empty;
            public Func<int> FpsProvider { get; init; } = () => 15;
            public Action<double> Tick { get; init; } = _ => { };
            public bool IsEnabled { get; set; } = true;
            public long LastTickTs;
        }

        private sealed class AmbientAnimationHandle : IAmbientAnimationHandle
        {
            private readonly AmbientAnimationScheduler _owner;
            private readonly string _id;
            private bool _disposed;

            public AmbientAnimationHandle(AmbientAnimationScheduler owner, string id)
            {
                _owner = owner;
                _id = id;
            }

            public void SetEnabled(bool enabled)
            {
                if (_disposed)
                {
                    return;
                }

                _owner.SetEnabled(_id, enabled);
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _owner.Remove(_id);
            }
        }

        private readonly DispatcherTimer _timer;
        private readonly Dictionary<string, Channel> _channels = new(StringComparer.Ordinal);

        public AmbientAnimationScheduler(PerformanceTelemetryService telemetryService)
        {
            _telemetryService = telemetryService;
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(8)
            };
            _timer.Tick += TimerOnTick;
        }

        public IAmbientAnimationHandle Register(string id, Func<int> fpsProvider, Action<double> onTick)
        {
            _channels[id] = new Channel
            {
                Id = id,
                FpsProvider = fpsProvider,
                Tick = onTick
            };

            EnsureTimerState();
            return new AmbientAnimationHandle(this, id);
        }

        private void SetEnabled(string id, bool enabled)
        {
            if (_channels.TryGetValue(id, out var channel))
            {
                channel.IsEnabled = enabled;
            }

            EnsureTimerState();
        }

        private void Remove(string id)
        {
            _channels.Remove(id);
            EnsureTimerState();
        }

        private void EnsureTimerState()
        {
            if (_channels.Values.All(c => !c.IsEnabled))
            {
                _timer.Stop();
                return;
            }

            var maxFps = _channels.Values
                .Where(c => c.IsEnabled)
                .Select(c => Math.Max(1, c.FpsProvider()))
                .DefaultIfEmpty(15)
                .Max();
            var intervalMs = Math.Max(4, 1000.0 / maxFps);
            _timer.Interval = TimeSpan.FromMilliseconds(intervalMs);
            if (!_timer.IsEnabled)
            {
                _timer.Start();
            }
        }

        private void TimerOnTick(object? sender, EventArgs e)
        {
            var nowTs = Stopwatch.GetTimestamp();
            var nowSeconds = nowTs / (double)Stopwatch.Frequency;
            foreach (var channel in _channels.Values)
            {
                if (!channel.IsEnabled)
                {
                    continue;
                }

                var fps = Math.Max(1, channel.FpsProvider());
                var intervalTicks = Stopwatch.Frequency / (double)fps;
                if (channel.LastTickTs != 0 && (nowTs - channel.LastTickTs) < intervalTicks)
                {
                    continue;
                }

                channel.LastTickTs = nowTs;
                channel.Tick(nowSeconds);
                _telemetryService.RecordAmbientFrame(channel.Id);
            }
        }
    }
}
