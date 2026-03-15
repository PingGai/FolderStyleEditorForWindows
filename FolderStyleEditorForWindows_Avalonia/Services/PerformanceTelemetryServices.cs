using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;

namespace FolderStyleEditorForWindows.Services
{
    public sealed class PerformanceTelemetryService : INotifyPropertyChanged, IDisposable
    {
        private readonly FrameRateSettings _settings;
        private readonly DispatcherTimer _publishTimer;
        private readonly Process _currentProcess;
        private readonly int _processorCount;
        private readonly object _metricsSync = new();
        private readonly Dictionary<string, PerformanceCounter> _gpuCounters = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PerformanceCounter> _appGpuCounters = new(StringComparer.OrdinalIgnoreCase);
        private PerformanceCounter? _systemCpuCounter;
        private GpuTelemetryBackend _gpuTelemetryBackend = GpuTelemetryBackend.Unknown;
        private DateTime _lastGpuBackendProbeUtc = DateTime.MinValue;
        private DateTime _sampleWindowStartedUtc = DateTime.UtcNow;
        private DateTime _lastCpuSampleUtc = DateTime.UtcNow;
        private DateTime _lastMetricsSampleUtc = DateTime.MinValue;
        private DateTime _lastGpuMetricsSampleUtc = DateTime.MinValue;
        private DateTime _lastGpuCounterRefreshUtc = DateTime.MinValue;
        private TimeSpan _lastProcessCpuTime;
        private int _metricsSamplingState;
        private bool _isSuspended;
        private bool _isDebugSessionActive;
        private bool _isMemoryProfilingActive;
        private bool _disposed;
        private bool _isGpuTelemetryPending;
        private long _foregroundFrames;
        private long _staticFrames;
        private long _backgroundAmbientFrames;
        private long _homeTitleAmbientFrames;
        private long _adminTitleAmbientFrames;
        private int _windowApproxFps;
        private int _foregroundFps;
        private int _staticFps;
        private int _backgroundAmbientFps;
        private int _homeTitleAmbientFps;
        private int _adminTitleAmbientFps;
        private string _renderMode = "Static";
        private double _appCpuUsagePercent;
        private double _systemCpuUsagePercent;
        private double _appGpuUsagePercent;
        private double _systemGpuUsagePercent;
        private double _telemetrySampleOverheadPercent;
        private bool _isAppGpuAvailable;
        private bool _isSystemGpuAvailable;

        private const string GpuWmiQuery = "SELECT Name, UtilizationPercentage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine";
        private static readonly TimeSpan GpuBackendRetryInterval = TimeSpan.FromMinutes(3);
        private static readonly TimeSpan CpuMetricsInterval = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan GpuMetricsInterval = TimeSpan.FromSeconds(2);
        private static readonly WaitCallback QueueProcessMetricsSampleCallback = static state =>
            ((PerformanceTelemetryService)state!).RunQueuedProcessMetricsSample();
        private static readonly WaitCallback DisposeProcessMetricResourcesCallback = static state =>
        {
            var tuple = ((PerformanceTelemetryService Service, bool Force))state!;
            tuple.Service.DisposeProcessMetricResources(tuple.Force);
        };
        private DateTime _queuedMetricsSampleUtc;
        private bool _queuedMetricsIncludeGpu;

        public event PropertyChangedEventHandler? PropertyChanged;

        public PerformanceTelemetryService(FrameRateSettings settings)
        {
            _settings = settings;
            _currentProcess = Process.GetCurrentProcess();
            _processorCount = Math.Max(1, Environment.ProcessorCount);
            _lastProcessCpuTime = _currentProcess.TotalProcessorTime;

            _settings.PropertyChanged += Settings_PropertyChanged;
            _publishTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _publishTimer.Tick += PublishTimer_Tick;
            RefreshPublishTimerState();
        }

        public bool IsMonitorVisible => _settings.ShowPerformanceMonitor;
        public bool IsDetailedMonitorVisible => _settings.ShowPerformanceMonitor && _settings.ShowDetailedPerformanceMonitor;
        public int WindowApproxFps { get => _windowApproxFps; private set => SetField(ref _windowApproxFps, value); }
        public int ForegroundFps { get => _foregroundFps; private set => SetField(ref _foregroundFps, value); }
        public int StaticFps { get => _staticFps; private set => SetField(ref _staticFps, value); }
        public int BackgroundAmbientFps { get => _backgroundAmbientFps; private set => SetField(ref _backgroundAmbientFps, value); }
        public int HomeTitleAmbientFps { get => _homeTitleAmbientFps; private set => SetField(ref _homeTitleAmbientFps, value); }
        public int AdminTitleAmbientFps { get => _adminTitleAmbientFps; private set => SetField(ref _adminTitleAmbientFps, value); }
        public string RenderMode { get => _renderMode; private set => SetField(ref _renderMode, value); }
        public double AppCpuUsagePercent { get => _appCpuUsagePercent; private set => SetField(ref _appCpuUsagePercent, value); }
        public double SystemCpuUsagePercent { get => _systemCpuUsagePercent; private set => SetField(ref _systemCpuUsagePercent, value); }
        public double AppGpuUsagePercent { get => _appGpuUsagePercent; private set => SetField(ref _appGpuUsagePercent, value); }
        public double SystemGpuUsagePercent { get => _systemGpuUsagePercent; private set => SetField(ref _systemGpuUsagePercent, value); }
        public double TelemetrySampleOverheadPercent { get => _telemetrySampleOverheadPercent; private set => SetField(ref _telemetrySampleOverheadPercent, value); }
        public bool IsAppGpuAvailable { get => _isAppGpuAvailable; private set => SetField(ref _isAppGpuAvailable, value); }
        public bool IsSystemGpuAvailable { get => _isSystemGpuAvailable; private set => SetField(ref _isSystemGpuAvailable, value); }
        public bool IsGpuTelemetryPending { get => _isGpuTelemetryPending; private set => SetField(ref _isGpuTelemetryPending, value); }

        public void RecordForegroundFrame()
        {
            Interlocked.Increment(ref _foregroundFrames);
        }

        public void RecordStaticFrame()
        {
            Interlocked.Increment(ref _staticFrames);
        }

        public void RecordAmbientFrame(string channelId)
        {
            switch (channelId)
            {
                case "main-background-flow":
                    Interlocked.Increment(ref _backgroundAmbientFrames);
                    break;
                case "home-title-gradient":
                    Interlocked.Increment(ref _homeTitleAmbientFrames);
                    break;
                case "edit-admin-title-gradient":
                    Interlocked.Increment(ref _adminTitleAmbientFrames);
                    break;
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _publishTimer.Stop();
            _publishTimer.Tick -= PublishTimer_Tick;
            _settings.PropertyChanged -= Settings_PropertyChanged;
            StopProcessMetricSampling(clearValues: false);
            _currentProcess.Dispose();
        }

        public void SetSuspended(bool suspended)
        {
            if (_isSuspended == suspended || _disposed)
            {
                return;
            }

            _isSuspended = suspended;
            if (suspended)
            {
                _publishTimer.Stop();
                StopProcessMetricSamplingAsync(clearValues: false);
                return;
            }

            ResetPublishWindow();
            RefreshPublishTimerState();
        }

        public void SetDebugSessionActive(bool active)
        {
            if (_isDebugSessionActive == active || _disposed)
            {
                return;
            }

            _isDebugSessionActive = active;
            RefreshPublishTimerState();
        }

        public void SetMemoryProfilingActive(bool active)
        {
            if (_isMemoryProfilingActive == active || _disposed)
            {
                return;
            }

            _isMemoryProfilingActive = active;
            RefreshPublishTimerState();
        }

        private void PublishTimer_Tick(object? sender, EventArgs e)
        {
            var now = DateTime.UtcNow;
            var elapsedSeconds = Math.Max(0.001, (now - _sampleWindowStartedUtc).TotalSeconds);
            _sampleWindowStartedUtc = now;

            ForegroundFps = ToFps(Interlocked.Exchange(ref _foregroundFrames, 0), elapsedSeconds);
            StaticFps = ToFps(Interlocked.Exchange(ref _staticFrames, 0), elapsedSeconds);
            BackgroundAmbientFps = ToFps(Interlocked.Exchange(ref _backgroundAmbientFrames, 0), elapsedSeconds);
            HomeTitleAmbientFps = ToFps(Interlocked.Exchange(ref _homeTitleAmbientFrames, 0), elapsedSeconds);
            AdminTitleAmbientFps = ToFps(Interlocked.Exchange(ref _adminTitleAmbientFrames, 0), elapsedSeconds);
            WindowApproxFps = Math.Max(
                Math.Max(ForegroundFps, StaticFps),
                Math.Max(BackgroundAmbientFps, Math.Max(HomeTitleAmbientFps, AdminTitleAmbientFps)));
            RenderMode = ResolveRenderMode();

            if (!ShouldSampleProcessMetrics())
            {
                StopProcessMetricSampling(clearValues: true);
                return;
            }

            if (now - _lastMetricsSampleUtc < CpuMetricsInterval)
            {
                return;
            }

            _lastMetricsSampleUtc = now;
            var includeGpuMetrics = now - _lastGpuMetricsSampleUtc >= GpuMetricsInterval;
            if (includeGpuMetrics)
            {
                _lastGpuMetricsSampleUtc = now;
            }

            QueueProcessMetricsSample(now, includeGpuMetrics);
        }

        private void InitializeSystemCpuCounter()
        {
            try
            {
                _systemCpuCounter = TryCreateCounter("Processor Information", "% Processor Utility", "_Total") ??
                                    TryCreateCounter("Processor", "% Processor Time", "_Total");
                _systemCpuCounter?.NextValue();
            }
            catch
            {
                DisposeCounter(_systemCpuCounter);
                _systemCpuCounter = null;
            }
        }

        private ProcessMetricsSnapshot CollectProcessMetricsSnapshot(DateTime now, bool includeGpuMetrics)
        {
            var elapsedMs = Math.Max(1.0, (now - _lastCpuSampleUtc).TotalMilliseconds);
            _lastCpuSampleUtc = now;

            _currentProcess.Refresh();
            var currentProcessCpuTime = _currentProcess.TotalProcessorTime;
            var processCpuDeltaMs = (currentProcessCpuTime - _lastProcessCpuTime).TotalMilliseconds;
            _lastProcessCpuTime = currentProcessCpuTime;

            var appCpuUsagePercent = Math.Clamp(processCpuDeltaMs / (elapsedMs * _processorCount) * 100.0, 0, 100);
            var systemCpuUsagePercent = 0.0;

            if (_systemCpuCounter != null)
            {
                try
                {
                    systemCpuUsagePercent = Math.Clamp(_systemCpuCounter.NextValue(), 0, 100);
                }
                catch
                {
                    DisposeCounter(_systemCpuCounter);
                    _systemCpuCounter = null;
                    systemCpuUsagePercent = 0;
                }
            }

            var systemGpuMax = 0.0;
            var appGpuMax = 0.0;
            var appAvailable = IsAppGpuAvailable;
            var systemAvailable = IsSystemGpuAvailable;

            if (includeGpuMetrics)
            {
                if (_gpuTelemetryBackend is GpuTelemetryBackend.Unknown or GpuTelemetryBackend.PerformanceCounter &&
                    now - _lastGpuCounterRefreshUtc >= TimeSpan.FromSeconds(20))
                {
                    RefreshGpuCounters();
                }

                var gpuSnapshot = CollectGpuUsageSnapshot(now);
                systemGpuMax = gpuSnapshot.SystemGpuPercent;
                appGpuMax = gpuSnapshot.AppGpuPercent;
                appAvailable = gpuSnapshot.IsAppGpuAvailable;
                systemAvailable = gpuSnapshot.IsSystemGpuAvailable;
            }

            return new ProcessMetricsSnapshot(
                appCpuUsagePercent,
                systemCpuUsagePercent,
                includeGpuMetrics && appAvailable ? Math.Clamp(appGpuMax, 0, 100) : AppGpuUsagePercent,
                includeGpuMetrics && systemAvailable ? Math.Clamp(systemGpuMax, 0, 100) : SystemGpuUsagePercent,
                0,
                includeGpuMetrics ? appAvailable : IsAppGpuAvailable,
                includeGpuMetrics ? systemAvailable : IsSystemGpuAvailable,
                includeGpuMetrics);
        }

        private void RefreshGpuCounters()
        {
            _lastGpuCounterRefreshUtc = DateTime.UtcNow;
            ReplaceCounters(_gpuCounters, EnumerateGpuCounters(instanceName => true));
            ReplaceCounters(_appGpuCounters, EnumerateGpuCounters(instanceName => instanceName.Contains($"pid_{_currentProcess.Id}_", StringComparison.OrdinalIgnoreCase)));
            _gpuTelemetryBackend = _appGpuCounters.Count > 0 || _gpuCounters.Count > 0
                ? GpuTelemetryBackend.PerformanceCounter
                : GpuTelemetryBackend.Unknown;
            _lastGpuBackendProbeUtc = _lastGpuCounterRefreshUtc;
        }

        private GpuUsageSnapshot CollectGpuUsageSnapshot(DateTime now)
        {
            if (_gpuTelemetryBackend == GpuTelemetryBackend.PerformanceCounter)
            {
                var snapshot = CollectPerformanceCounterGpuUsageSnapshot();
                if (snapshot.IsAppGpuAvailable || snapshot.IsSystemGpuAvailable)
                {
                    return snapshot;
                }

                _gpuTelemetryBackend = GpuTelemetryBackend.Unknown;
            }

            if (_gpuTelemetryBackend == GpuTelemetryBackend.Wmi)
            {
                var snapshot = CollectWmiGpuUsageSnapshot();
                if (snapshot.IsAppGpuAvailable || snapshot.IsSystemGpuAvailable)
                {
                    return snapshot;
                }

                _gpuTelemetryBackend = GpuTelemetryBackend.Unsupported;
                _lastGpuBackendProbeUtc = now;
                return GpuUsageSnapshot.Empty;
            }

            if (_gpuTelemetryBackend == GpuTelemetryBackend.Unsupported &&
                now - _lastGpuBackendProbeUtc < GpuBackendRetryInterval)
            {
                return GpuUsageSnapshot.Empty;
            }

            var wmiSnapshot = CollectWmiGpuUsageSnapshot();
            if (wmiSnapshot.IsAppGpuAvailable || wmiSnapshot.IsSystemGpuAvailable)
            {
                _gpuTelemetryBackend = GpuTelemetryBackend.Wmi;
                _lastGpuBackendProbeUtc = now;
                return wmiSnapshot;
            }

            _gpuTelemetryBackend = GpuTelemetryBackend.Unsupported;
            _lastGpuBackendProbeUtc = now;
            return GpuUsageSnapshot.Empty;
        }

        private GpuUsageSnapshot CollectPerformanceCounterGpuUsageSnapshot()
        {
            var systemGpuMax = 0.0;
            var appGpuMax = 0.0;
            var appAvailable = _appGpuCounters.Count > 0;
            var systemAvailable = _gpuCounters.Count > 0;

            foreach (var counter in _gpuCounters.Values.ToArray())
            {
                try
                {
                    systemGpuMax = Math.Max(systemGpuMax, counter.NextValue());
                }
                catch
                {
                    systemAvailable = false;
                }
            }

            foreach (var counter in _appGpuCounters.Values.ToArray())
            {
                try
                {
                    appGpuMax = Math.Max(appGpuMax, counter.NextValue());
                }
                catch
                {
                    appAvailable = false;
                }
            }

            return new GpuUsageSnapshot(
                appAvailable ? Math.Clamp(appGpuMax, 0, 100) : 0,
                systemAvailable ? Math.Clamp(systemGpuMax, 0, 100) : 0,
                appAvailable,
                systemAvailable);
        }

        private GpuUsageSnapshot CollectWmiGpuUsageSnapshot()
        {
            if (!OperatingSystem.IsWindows())
            {
                return GpuUsageSnapshot.Empty;
            }

            try
            {
                using var searcher = new ManagementObjectSearcher(GpuWmiQuery);
                using var results = searcher.Get();
                var appMarker = $"pid_{_currentProcess.Id}_";
                var appGpuMax = 0.0;
                var systemGpuMax = 0.0;
                var appAvailable = false;
                var systemAvailable = false;

                foreach (var instance in results.Cast<ManagementObject>())
                {
                    var name = instance["Name"] as string;
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    if (!TryReadGpuPercentage(instance["UtilizationPercentage"], out var percent))
                    {
                        continue;
                    }

                    systemGpuMax = Math.Max(systemGpuMax, percent);
                    systemAvailable = true;

                    if (name.Contains(appMarker, StringComparison.OrdinalIgnoreCase))
                    {
                        appGpuMax = Math.Max(appGpuMax, percent);
                        appAvailable = true;
                    }
                }

                return new GpuUsageSnapshot(
                    appAvailable ? Math.Clamp(appGpuMax, 0, 100) : 0,
                    systemAvailable ? Math.Clamp(systemGpuMax, 0, 100) : 0,
                    appAvailable,
                    systemAvailable);
            }
            catch
            {
                return GpuUsageSnapshot.Empty;
            }
        }

        private static bool TryReadGpuPercentage(object? value, out double percent)
        {
            percent = 0;
            switch (value)
            {
                case byte b:
                    percent = b;
                    return true;
                case ushort us:
                    percent = us;
                    return true;
                case uint ui:
                    percent = ui;
                    return true;
                case ulong ul:
                    percent = ul;
                    return true;
                case short s:
                    percent = s;
                    return true;
                case int i:
                    percent = i;
                    return true;
                case long l:
                    percent = l;
                    return true;
                case float f:
                    percent = f;
                    return true;
                case double d:
                    percent = d;
                    return true;
                case decimal m:
                    percent = (double)m;
                    return true;
                case string text when double.TryParse(text, out var parsed):
                    percent = parsed;
                    return true;
                default:
                    return false;
            }
        }

        private IEnumerable<KeyValuePair<string, PerformanceCounter>> EnumerateGpuCounters(Func<string, bool> predicate)
        {
            var counters = new List<KeyValuePair<string, PerformanceCounter>>();
            if (!OperatingSystem.IsWindows())
            {
                return counters;
            }

            PerformanceCounterCategory category;
            try
            {
                category = new PerformanceCounterCategory("GPU Engine");
            }
            catch
            {
                return counters;
            }

            string[] instanceNames;
            try
            {
                instanceNames = category.GetInstanceNames();
            }
            catch
            {
                return counters;
            }

            foreach (var instanceName in instanceNames)
            {
                if (!predicate(instanceName))
                {
                    continue;
                }

                PerformanceCounter? counter = null;
                try
                {
                    counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", instanceName, true);
                    counter.NextValue();
                    counters.Add(new KeyValuePair<string, PerformanceCounter>(instanceName, counter));
                }
                catch
                {
                    DisposeCounter(counter);
                }
            }

            return counters;
        }

        private void ReplaceCounters(Dictionary<string, PerformanceCounter> target, IEnumerable<KeyValuePair<string, PerformanceCounter>> next)
        {
            foreach (var counter in target.Values)
            {
                DisposeCounter(counter);
            }

            target.Clear();
            foreach (var pair in next)
            {
                target[pair.Key] = pair.Value;
            }
        }

        private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FrameRateSettings.ShowPerformanceMonitor) ||
                e.PropertyName == nameof(FrameRateSettings.ShowDetailedPerformanceMonitor) ||
                e.PropertyName == nameof(FrameRateSettings.ShowComponentFpsBadges))
            {
                RefreshPublishTimerState();
                if (ShouldSampleProcessMetrics())
                {
                    IsGpuTelemetryPending = true;
                    _lastMetricsSampleUtc = DateTime.MinValue;
                    _lastGpuMetricsSampleUtc = DateTime.MinValue;
                    QueueProcessMetricsSample(DateTime.UtcNow, includeGpuMetrics: true);
                }
                else
                {
                    IsGpuTelemetryPending = false;
                    StopProcessMetricSamplingAsync(clearValues: true);
                }

                OnPropertyChanged(nameof(IsMonitorVisible));
                OnPropertyChanged(nameof(IsDetailedMonitorVisible));
            }
        }

        private bool ShouldSampleProcessMetrics()
        {
            return !_isSuspended && (_settings.ShowPerformanceMonitor || _isMemoryProfilingActive);
        }

        private bool ShouldPublishTelemetry()
        {
            return !_isSuspended &&
                   (_settings.ShowPerformanceMonitor ||
                    _settings.ShowComponentFpsBadges ||
                    _isDebugSessionActive ||
                    _isMemoryProfilingActive);
        }

        private void RefreshPublishTimerState()
        {
            if (_disposed)
            {
                return;
            }

            if (!ShouldPublishTelemetry())
            {
                _publishTimer.Stop();
                StopProcessMetricSamplingAsync(clearValues: !_settings.ShowPerformanceMonitor);
                return;
            }

            ResetPublishWindow();
            if (!_publishTimer.IsEnabled)
            {
                _publishTimer.Start();
            }
        }

        private void ResetPublishWindow()
        {
            _sampleWindowStartedUtc = DateTime.UtcNow;
            _lastCpuSampleUtc = DateTime.UtcNow;
            _lastMetricsSampleUtc = DateTime.MinValue;
            _lastGpuMetricsSampleUtc = DateTime.MinValue;
            IsGpuTelemetryPending = ShouldSampleProcessMetrics();
            Interlocked.Exchange(ref _foregroundFrames, 0);
            Interlocked.Exchange(ref _staticFrames, 0);
            Interlocked.Exchange(ref _backgroundAmbientFrames, 0);
            Interlocked.Exchange(ref _homeTitleAmbientFrames, 0);
            Interlocked.Exchange(ref _adminTitleAmbientFrames, 0);
        }

        private void QueueProcessMetricsSample(DateTime now, bool includeGpuMetrics)
        {
            if (_disposed || Interlocked.CompareExchange(ref _metricsSamplingState, 1, 0) != 0)
            {
                return;
            }

            _queuedMetricsSampleUtc = now;
            _queuedMetricsIncludeGpu = includeGpuMetrics;
            ThreadPool.UnsafeQueueUserWorkItem(QueueProcessMetricsSampleCallback, this);
        }

        private void RunQueuedProcessMetricsSample()
        {
            try
            {
                ProcessMetricsSnapshot snapshot;
                var sampleStopwatch = Stopwatch.StartNew();
                lock (_metricsSync)
                {
                    if (_disposed || !ShouldSampleProcessMetrics())
                    {
                        snapshot = ProcessMetricsSnapshot.Empty;
                    }
                    else
                    {
                        EnsureProcessMetricSamplingStartedCore();
                        snapshot = CollectProcessMetricsSnapshot(_queuedMetricsSampleUtc, _queuedMetricsIncludeGpu);
                    }
                }

                sampleStopwatch.Stop();
                var sampleOverheadPercent = Math.Clamp(
                    sampleStopwatch.Elapsed.TotalMilliseconds / 1000.0 / _processorCount * 100.0,
                    0,
                    100);
                snapshot = snapshot with { TelemetrySampleOverheadPercent = sampleOverheadPercent };

                Dispatcher.UIThread.Post(() =>
                {
                    if (_disposed)
                    {
                        return;
                    }

                    if (!ShouldSampleProcessMetrics())
                    {
                        StopProcessMetricSamplingAsync(clearValues: true);
                        return;
                    }

                    ApplyProcessMetricsSnapshot(snapshot);
                });
            }
            catch
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_disposed)
                    {
                        return;
                    }

                    AppCpuUsagePercent = 0;
                    SystemCpuUsagePercent = 0;
                    AppGpuUsagePercent = 0;
                    SystemGpuUsagePercent = 0;
                    TelemetrySampleOverheadPercent = 0;
                    IsAppGpuAvailable = false;
                    IsSystemGpuAvailable = false;
                    IsGpuTelemetryPending = false;
                });
            }
            finally
            {
                Interlocked.Exchange(ref _metricsSamplingState, 0);
            }
        }

        private void EnsureProcessMetricSamplingStartedCore()
        {
            var started = false;
            if (_systemCpuCounter == null)
            {
                InitializeSystemCpuCounter();
                started = true;
            }

            if (_gpuCounters.Count == 0 && _appGpuCounters.Count == 0)
            {
                RefreshGpuCounters();
                started = true;
            }

            if (!started)
            {
                return;
            }

            _lastCpuSampleUtc = DateTime.UtcNow;
            _lastProcessCpuTime = _currentProcess.TotalProcessorTime;
        }

        private void StopProcessMetricSampling(bool clearValues)
        {
            if (clearValues)
            {
                ClearProcessMetricValues();
            }

            DisposeProcessMetricResources(force: true);
        }

        private void StopProcessMetricSamplingAsync(bool clearValues)
        {
            if (clearValues)
            {
                ClearProcessMetricValues();
            }

            ThreadPool.UnsafeQueueUserWorkItem(DisposeProcessMetricResourcesCallback, (this, false));
        }

        private void DisposeProcessMetricResources(bool force)
        {
            lock (_metricsSync)
            {
                if (!force && !_disposed && ShouldSampleProcessMetrics())
                {
                    return;
                }

                DisposeCounter(_systemCpuCounter);
                _systemCpuCounter = null;

                foreach (var counter in _gpuCounters.Values)
                {
                    DisposeCounter(counter);
                }

                foreach (var counter in _appGpuCounters.Values)
                {
                    DisposeCounter(counter);
                }

                _gpuCounters.Clear();
                _appGpuCounters.Clear();
                _lastGpuCounterRefreshUtc = DateTime.MinValue;
                _lastMetricsSampleUtc = DateTime.MinValue;
                _gpuTelemetryBackend = GpuTelemetryBackend.Unknown;
                _lastGpuBackendProbeUtc = DateTime.MinValue;
            }
        }

        private void ClearProcessMetricValues()
        {
            AppCpuUsagePercent = 0;
            SystemCpuUsagePercent = 0;
            AppGpuUsagePercent = 0;
            SystemGpuUsagePercent = 0;
            TelemetrySampleOverheadPercent = 0;
            IsAppGpuAvailable = false;
            IsSystemGpuAvailable = false;
            IsGpuTelemetryPending = false;
        }

        private void ApplyProcessMetricsSnapshot(ProcessMetricsSnapshot snapshot)
        {
            TelemetrySampleOverheadPercent = snapshot.TelemetrySampleOverheadPercent;
            AppCpuUsagePercent = Math.Max(0, snapshot.AppCpuUsagePercent - snapshot.TelemetrySampleOverheadPercent);
            SystemCpuUsagePercent = snapshot.SystemCpuUsagePercent;
            if (snapshot.HasGpuUpdate)
            {
                IsGpuTelemetryPending = false;
                AppGpuUsagePercent = snapshot.AppGpuUsagePercent;
                SystemGpuUsagePercent = snapshot.SystemGpuUsagePercent;
                IsAppGpuAvailable = snapshot.IsAppGpuAvailable;
                IsSystemGpuAvailable = snapshot.IsSystemGpuAvailable;
            }
        }

        private string ResolveRenderMode()
        {
            var loc = LocalizationManager.Instance;
            if (_settings.CurrentForegroundTargetFps > 0)
            {
                var maxFps = _settings.UseDisplayRefreshRateAsMaxFps
                    ? _settings.DisplayRefreshRateHz
                    : Math.Min(_settings.DisplayRefreshRateHz, _settings.ManualMaxFps);
                return _settings.CurrentForegroundTargetFps >= maxFps
                    ? loc["PerformanceMonitor_Mode_Max"]
                    : loc["PerformanceMonitor_Mode_Active"];
            }

            if (WindowApproxFps > 0)
            {
                return loc["PerformanceMonitor_Mode_Ambient"];
            }

            return loc["PerformanceMonitor_Mode_Static"];
        }

        private static PerformanceCounter? TryCreateCounter(string category, string counterName, string instanceName)
        {
            try
            {
                return new PerformanceCounter(category, counterName, instanceName, true);
            }
            catch
            {
                return null;
            }
        }

        private static void DisposeCounter(PerformanceCounter? counter)
        {
            try
            {
                counter?.Dispose();
            }
            catch
            {
                // Ignore counter disposal failures.
            }
        }

        private static int ToFps(long frameCount, double elapsedSeconds)
        {
            return (int)Math.Round(frameCount / elapsedSeconds, MidpointRounding.AwayFromZero);
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged(string? propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private readonly record struct ProcessMetricsSnapshot(
            double AppCpuUsagePercent,
            double SystemCpuUsagePercent,
            double AppGpuUsagePercent,
            double SystemGpuUsagePercent,
            double TelemetrySampleOverheadPercent,
            bool IsAppGpuAvailable,
            bool IsSystemGpuAvailable,
            bool HasGpuUpdate)
        {
            public static ProcessMetricsSnapshot Empty => new(0, 0, 0, 0, 0, false, false, false);
        }

        private readonly record struct GpuUsageSnapshot(
            double AppGpuPercent,
            double SystemGpuPercent,
            bool IsAppGpuAvailable,
            bool IsSystemGpuAvailable)
        {
            public static GpuUsageSnapshot Empty => new(0, 0, false, false);
        }

        private enum GpuTelemetryBackend
        {
            Unknown,
            PerformanceCounter,
            Wmi,
            Unsupported
        }
    }

    public sealed class PerformanceMonitorSessionState : INotifyPropertyChanged
    {
        private const double DefaultMargin = 12;
        private const double DefaultInitialTop = 56;
        private const double SnapThreshold = 28;
        private double _left = DefaultMargin;
        private double _top = DefaultInitialTop;
        private Point _dragStartPointer;
        private double _dragStartLeft;
        private double _dragStartTop;
        private bool _isDragging;

        public event PropertyChangedEventHandler? PropertyChanged;

        public double Left
        {
            get => _left;
            private set => SetField(ref _left, value);
        }

        public double Top
        {
            get => _top;
            private set => SetField(ref _top, value);
        }

        public void Reset()
        {
            Left = DefaultMargin;
            Top = DefaultInitialTop;
        }

        public void BeginDrag(Point pointerPositionInWindow)
        {
            _dragStartPointer = pointerPositionInWindow;
            _dragStartLeft = Left;
            _dragStartTop = Top;
            _isDragging = true;
        }

        public void UpdateDrag(Point pointerPositionInWindow, Size hostSize, Size monitorSize)
        {
            if (!_isDragging)
            {
                return;
            }

            var delta = pointerPositionInWindow - _dragStartPointer;
            SetPosition(_dragStartLeft + delta.X, _dragStartTop + delta.Y, hostSize, monitorSize);
        }

        public void EndDrag(Size hostSize, Size monitorSize)
        {
            if (!_isDragging)
            {
                return;
            }

            _isDragging = false;
            var maxLeft = GetMaxCoordinate(hostSize.Width, monitorSize.Width);
            var maxTop = GetMaxCoordinate(hostSize.Height, monitorSize.Height);
            var targetLeft = Left;
            var targetTop = Top;

            if (Math.Abs(Left - DefaultMargin) <= SnapThreshold)
            {
                targetLeft = DefaultMargin;
            }
            else if (Math.Abs(Left - maxLeft) <= SnapThreshold)
            {
                targetLeft = maxLeft;
            }

            if (Math.Abs(Top - DefaultMargin) <= SnapThreshold)
            {
                targetTop = DefaultMargin;
            }
            else if (Math.Abs(Top - maxTop) <= SnapThreshold)
            {
                targetTop = maxTop;
            }

            Left = targetLeft;
            Top = targetTop;
        }

        private void SetPosition(double left, double top, Size hostSize, Size monitorSize)
        {
            Left = ClampCoordinate(left, hostSize.Width, monitorSize.Width);
            Top = ClampCoordinate(top, hostSize.Height, monitorSize.Height);
        }

        private static double ClampCoordinate(double value, double hostSize, double monitorSize)
        {
            var min = DefaultMargin;
            var max = GetMaxCoordinate(hostSize, monitorSize);
            if (max < min)
            {
                return Math.Max(0, Math.Min(value, Math.Max(0, hostSize - monitorSize)));
            }

            return Math.Max(min, Math.Min(max, value));
        }

        private static double GetMaxCoordinate(double hostSize, double monitorSize)
        {
            return Math.Max(DefaultMargin, hostSize - monitorSize - DefaultMargin);
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    public sealed class PerformanceMonitorViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly FrameRateSettings _settings;
        private readonly PerformanceTelemetryService _telemetry;
        private readonly PerformanceMonitorSessionState _sessionState;
        private bool _isHostActive = true;
        private bool _isHostSuspended;

        public event PropertyChangedEventHandler? PropertyChanged;

        public PerformanceMonitorViewModel(
            FrameRateSettings settings,
            PerformanceTelemetryService telemetry,
            PerformanceMonitorSessionState sessionState)
        {
            _settings = settings;
            _telemetry = telemetry;
            _sessionState = sessionState;

            _settings.PropertyChanged += Settings_PropertyChanged;
            _telemetry.PropertyChanged += Source_PropertyChanged;
            _sessionState.PropertyChanged += Source_PropertyChanged;
        }

        public bool IsVisible => _settings.ShowPerformanceMonitor;
        public bool IsOpenInHost => _settings.ShowPerformanceMonitor && _isHostActive && !_isHostSuspended;
        public bool IsDetailedVisible => _settings.ShowPerformanceMonitor && _settings.ShowDetailedPerformanceMonitor;
        public double Left => _sessionState.Left;
        public double Top => _sessionState.Top;
        public string RenderMode => _telemetry.RenderMode;
        public int WindowApproxFps => _telemetry.WindowApproxFps;
        public string AppCpuDisplay => FormatPercent(_telemetry.AppCpuUsagePercent);
        public string SystemCpuDisplay => FormatPercent(_telemetry.SystemCpuUsagePercent);
        public string AppGpuDisplay => _telemetry.IsGpuTelemetryPending
            ? LocalizationManager.Instance["PerformanceMonitor_Value_Detecting"]
            : _telemetry.IsAppGpuAvailable
                ? FormatPercent(_telemetry.AppGpuUsagePercent)
                : LocalizationManager.Instance["PerformanceMonitor_Value_Unavailable"];
        public string SystemGpuDisplay => _telemetry.IsGpuTelemetryPending
            ? LocalizationManager.Instance["PerformanceMonitor_Value_Detecting"]
            : _telemetry.IsSystemGpuAvailable
                ? FormatPercent(_telemetry.SystemGpuUsagePercent)
                : LocalizationManager.Instance["PerformanceMonitor_Value_Unavailable"];
        public string ForegroundDisplay => FormatFps(_telemetry.ForegroundFps);
        public string StaticDisplay => FormatFps(_telemetry.StaticFps);
        public string BackgroundAmbientDisplay => FormatFps(_telemetry.BackgroundAmbientFps);
        public string HomeTitleAmbientDisplay => FormatFps(_telemetry.HomeTitleAmbientFps);
        public string AdminTitleAmbientDisplay => FormatFps(_telemetry.AdminTitleAmbientFps);

        public void ResetPosition()
        {
            _sessionState.Reset();
        }

        public void Hide()
        {
            _settings.ShowPerformanceMonitor = false;
        }

        public void SetHostActive(bool isActive)
        {
            if (_isHostActive == isActive)
            {
                return;
            }

            _isHostActive = isActive;
            OnPropertyChanged(nameof(IsOpenInHost));
        }

        public void SetHostSuspended(bool suspended)
        {
            if (_isHostSuspended == suspended)
            {
                return;
            }

            _isHostSuspended = suspended;
            OnPropertyChanged(nameof(IsOpenInHost));
        }

        public void BeginDrag(Point pointerPositionInWindow)
        {
            _sessionState.BeginDrag(pointerPositionInWindow);
        }

        public void UpdateDrag(Point pointerPositionInWindow, Size hostSize, Size monitorSize)
        {
            _sessionState.UpdateDrag(pointerPositionInWindow, hostSize, monitorSize);
        }

        public void EndDrag(Size hostSize, Size monitorSize)
        {
            _sessionState.EndDrag(hostSize, monitorSize);
        }

        public void Dispose()
        {
            _settings.PropertyChanged -= Settings_PropertyChanged;
            _telemetry.PropertyChanged -= Source_PropertyChanged;
            _sessionState.PropertyChanged -= Source_PropertyChanged;
        }

        private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FrameRateSettings.ShowPerformanceMonitor))
            {
                if (_settings.ShowPerformanceMonitor)
                {
                    _sessionState.Reset();
                }

                OnPropertyChanged(nameof(IsVisible));
                OnPropertyChanged(nameof(IsOpenInHost));
                OnPropertyChanged(nameof(IsDetailedVisible));
                OnPropertyChanged(nameof(Left));
                OnPropertyChanged(nameof(Top));
                return;
            }

            if (e.PropertyName == nameof(FrameRateSettings.ShowDetailedPerformanceMonitor))
            {
                OnPropertyChanged(nameof(IsDetailedVisible));
            }
        }

        private void Source_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(PerformanceTelemetryService.WindowApproxFps):
                    OnPropertyChanged(nameof(WindowApproxFps));
                    break;
                case nameof(PerformanceTelemetryService.RenderMode):
                    OnPropertyChanged(nameof(RenderMode));
                    break;
                case nameof(PerformanceTelemetryService.AppCpuUsagePercent):
                    OnPropertyChanged(nameof(AppCpuDisplay));
                    break;
                case nameof(PerformanceTelemetryService.SystemCpuUsagePercent):
                    OnPropertyChanged(nameof(SystemCpuDisplay));
                    break;
                case nameof(PerformanceTelemetryService.AppGpuUsagePercent):
                case nameof(PerformanceTelemetryService.IsAppGpuAvailable):
                case nameof(PerformanceTelemetryService.IsGpuTelemetryPending):
                    OnPropertyChanged(nameof(AppGpuDisplay));
                    OnPropertyChanged(nameof(SystemGpuDisplay));
                    break;
                case nameof(PerformanceTelemetryService.SystemGpuUsagePercent):
                case nameof(PerformanceTelemetryService.IsSystemGpuAvailable):
                    OnPropertyChanged(nameof(SystemGpuDisplay));
                    break;
                case nameof(PerformanceTelemetryService.ForegroundFps):
                    OnPropertyChanged(nameof(ForegroundDisplay));
                    break;
                case nameof(PerformanceTelemetryService.StaticFps):
                    OnPropertyChanged(nameof(StaticDisplay));
                    break;
                case nameof(PerformanceTelemetryService.BackgroundAmbientFps):
                    OnPropertyChanged(nameof(BackgroundAmbientDisplay));
                    break;
                case nameof(PerformanceTelemetryService.HomeTitleAmbientFps):
                    OnPropertyChanged(nameof(HomeTitleAmbientDisplay));
                    break;
                case nameof(PerformanceTelemetryService.AdminTitleAmbientFps):
                    OnPropertyChanged(nameof(AdminTitleAmbientDisplay));
                    break;
                case nameof(PerformanceMonitorSessionState.Left):
                    OnPropertyChanged(nameof(Left));
                    break;
                case nameof(PerformanceMonitorSessionState.Top):
                    OnPropertyChanged(nameof(Top));
                    break;
                case nameof(PerformanceTelemetryService.IsMonitorVisible):
                    OnPropertyChanged(nameof(IsVisible));
                    break;
                case nameof(PerformanceTelemetryService.IsDetailedMonitorVisible):
                    OnPropertyChanged(nameof(IsDetailedVisible));
                    break;
            }
        }

        private static string FormatPercent(double value)
        {
            return $"{Math.Round(value, 1):0.#}%";
        }

        private static string FormatFps(int value)
        {
            return value.ToString();
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public sealed class ComponentFpsBadgeSource : INotifyPropertyChanged, IDisposable
    {
        private readonly FrameRateSettings _settings;
        private readonly PerformanceTelemetryService _telemetry;
        private bool _toastVisible;
        private bool _dragOverlayVisible;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ComponentFpsBadgeSource(FrameRateSettings settings, PerformanceTelemetryService telemetry)
        {
            _settings = settings;
            _telemetry = telemetry;
            _settings.PropertyChanged += Settings_PropertyChanged;
            _telemetry.PropertyChanged += Telemetry_PropertyChanged;
        }

        public bool IsEnabled => _settings.ShowComponentFpsBadges;
        public string ForegroundDisplay => _telemetry.ForegroundFps.ToString();
        public string StaticDisplay => _telemetry.StaticFps.ToString();
        public string BackgroundDisplay => _telemetry.BackgroundAmbientFps.ToString();
        public string HomeTitleDisplay => _telemetry.HomeTitleAmbientFps.ToString();
        public string AdminTitleDisplay => _telemetry.AdminTitleAmbientFps.ToString();
        public string ToastDisplay => (_toastVisible ? _telemetry.ForegroundFps : 0).ToString();
        public string DragOverlayDisplay => (_dragOverlayVisible ? _telemetry.ForegroundFps : 0).ToString();
        public bool IsToastBadgeVisible => IsEnabled && _toastVisible;
        public bool IsDragOverlayBadgeVisible => IsEnabled && _dragOverlayVisible;

        public void SetToastVisible(bool visible)
        {
            if (_toastVisible == visible)
            {
                return;
            }

            _toastVisible = visible;
            OnPropertyChanged(nameof(ToastDisplay));
            OnPropertyChanged(nameof(IsToastBadgeVisible));
        }

        public void SetDragOverlayVisible(bool visible)
        {
            if (_dragOverlayVisible == visible)
            {
                return;
            }

            _dragOverlayVisible = visible;
            OnPropertyChanged(nameof(DragOverlayDisplay));
            OnPropertyChanged(nameof(IsDragOverlayBadgeVisible));
        }

        public void Dispose()
        {
            _settings.PropertyChanged -= Settings_PropertyChanged;
            _telemetry.PropertyChanged -= Telemetry_PropertyChanged;
        }

        private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FrameRateSettings.ShowComponentFpsBadges))
            {
                OnPropertyChanged(nameof(IsEnabled));
                OnPropertyChanged(nameof(IsToastBadgeVisible));
                OnPropertyChanged(nameof(IsDragOverlayBadgeVisible));
            }
        }

        private void Telemetry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(PerformanceTelemetryService.ForegroundFps):
                    OnPropertyChanged(nameof(ForegroundDisplay));
                    OnPropertyChanged(nameof(ToastDisplay));
                    OnPropertyChanged(nameof(DragOverlayDisplay));
                    break;
                case nameof(PerformanceTelemetryService.StaticFps):
                    OnPropertyChanged(nameof(StaticDisplay));
                    break;
                case nameof(PerformanceTelemetryService.BackgroundAmbientFps):
                    OnPropertyChanged(nameof(BackgroundDisplay));
                    break;
                case nameof(PerformanceTelemetryService.HomeTitleAmbientFps):
                    OnPropertyChanged(nameof(HomeTitleDisplay));
                    break;
                case nameof(PerformanceTelemetryService.AdminTitleAmbientFps):
                    OnPropertyChanged(nameof(AdminTitleDisplay));
                    break;
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
