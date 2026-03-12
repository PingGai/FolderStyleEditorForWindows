using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FolderStyleEditorForWindows.ViewModels;

namespace FolderStyleEditorForWindows.Services
{
    public sealed class MemoryProfileService : IDisposable
    {
        private readonly FrameRateSettings _settings;
        private readonly PerformanceTelemetryService _telemetry;
        private readonly MainViewModel _viewModel;
        private readonly object _syncRoot = new();
        private CancellationTokenSource? _recordingCts;
        private Task? _recordingTask;
        private string? _outputPath;
        private string? _lastError;
        private int _sampleCount;
        private bool _disposed;

        public MemoryProfileService(
            FrameRateSettings settings,
            PerformanceTelemetryService telemetry,
            MainViewModel viewModel)
        {
            _settings = settings;
            _telemetry = telemetry;
            _viewModel = viewModel;
        }

        public bool IsRecording
        {
            get
            {
                lock (_syncRoot)
                {
                    return _recordingCts != null;
                }
            }
        }

        public string? OutputPath
        {
            get
            {
                lock (_syncRoot)
                {
                    return _outputPath;
                }
            }
        }

        public string? LastError
        {
            get
            {
                lock (_syncRoot)
                {
                    return _lastError;
                }
            }
        }

        public int SampleCount
        {
            get
            {
                lock (_syncRoot)
                {
                    return _sampleCount;
                }
            }
        }

        public string ProfilesDirectory => Path.Combine(ConfigManager.AppDataDirectory, "profiles");

        public void StartRecording()
        {
            lock (_syncRoot)
            {
                if (_disposed || _recordingCts != null)
                {
                    return;
                }

                Directory.CreateDirectory(ProfilesDirectory);
                _outputPath = Path.Combine(
                    ProfilesDirectory,
                    $"memory-profile-{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.csv");
                _lastError = null;
                _sampleCount = 0;
                _recordingCts = new CancellationTokenSource();
                _recordingTask = RunRecordingLoopAsync(_outputPath, _recordingCts.Token);
            }

            _telemetry.SetMemoryProfilingActive(true);
        }

        public void StopRecording()
        {
            CancellationTokenSource? cts;
            lock (_syncRoot)
            {
                cts = _recordingCts;
                _recordingCts = null;
            }

            cts?.Cancel();
            _telemetry.SetMemoryProfilingActive(false);
        }

        public void Dispose()
        {
            _disposed = true;
            StopRecording();
        }

        private async Task RunRecordingLoopAsync(string outputPath, CancellationToken cancellationToken)
        {
            try
            {
                await using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                await writer.WriteLineAsync("utc_iso,elapsed_seconds,managed_mb,heap_mb,fragmented_mb,total_allocated_mb,private_mb,working_set_mb,gen0,gen1,gen2,foreground_target_fps,ambient_target_fps,window_fps,render_mode,app_cpu_percent,app_gpu_percent,history_count,is_loading_icons,is_loading_indicator,pause_animations,show_performance_monitor,show_component_fps_badges,show_detailed_monitor");
                await writer.FlushAsync(cancellationToken);

                var startedUtc = DateTime.UtcNow;
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    var sample = CreateSample(startedUtc);
                    await writer.WriteLineAsync(sample);
                    await writer.FlushAsync(cancellationToken);
                    lock (_syncRoot)
                    {
                        _sampleCount++;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // normal stop
            }
            catch (Exception ex)
            {
                lock (_syncRoot)
                {
                    _lastError = ex.Message;
                }
            }
        }

        private string CreateSample(DateTime startedUtc)
        {
            using var process = Process.GetCurrentProcess();
            process.Refresh();
            var nowUtc = DateTime.UtcNow;
            var gcInfo = GC.GetGCMemoryInfo();

            return string.Join(",",
                nowUtc.ToString("O", CultureInfo.InvariantCulture),
                (nowUtc - startedUtc).TotalSeconds.ToString("F1", CultureInfo.InvariantCulture),
                BytesToMegabytes(GC.GetTotalMemory(false)).ToString("F1", CultureInfo.InvariantCulture),
                BytesToMegabytes(gcInfo.HeapSizeBytes).ToString("F1", CultureInfo.InvariantCulture),
                BytesToMegabytes(gcInfo.FragmentedBytes).ToString("F1", CultureInfo.InvariantCulture),
                BytesToMegabytes(GC.GetTotalAllocatedBytes(false)).ToString("F1", CultureInfo.InvariantCulture),
                BytesToMegabytes(process.PrivateMemorySize64).ToString("F1", CultureInfo.InvariantCulture),
                BytesToMegabytes(process.WorkingSet64).ToString("F1", CultureInfo.InvariantCulture),
                GC.CollectionCount(0).ToString(CultureInfo.InvariantCulture),
                GC.CollectionCount(1).ToString(CultureInfo.InvariantCulture),
                GC.CollectionCount(2).ToString(CultureInfo.InvariantCulture),
                _settings.CurrentForegroundTargetFps.ToString(CultureInfo.InvariantCulture),
                _settings.CurrentAmbientTargetFps.ToString(CultureInfo.InvariantCulture),
                _telemetry.WindowApproxFps.ToString(CultureInfo.InvariantCulture),
                CsvEscape(_telemetry.RenderMode),
                _telemetry.AppCpuUsagePercent.ToString("F1", CultureInfo.InvariantCulture),
                _telemetry.AppGpuUsagePercent.ToString("F1", CultureInfo.InvariantCulture),
                _viewModel.History.Count.ToString(CultureInfo.InvariantCulture),
                _viewModel.IsLoadingIcons ? "1" : "0",
                _viewModel.IsLoadingIconsIndicatorVisible ? "1" : "0",
                DebugRuntimeAnalysis.PauseAnimations ? "1" : "0",
                _settings.ShowPerformanceMonitor ? "1" : "0",
                _settings.ShowComponentFpsBadges ? "1" : "0",
                _settings.ShowDetailedPerformanceMonitor ? "1" : "0");
        }

        private static double BytesToMegabytes(long value)
        {
            return value / 1024d / 1024d;
        }

        private static string CsvEscape(string value)
        {
            if (!value.Contains(',') && !value.Contains('"'))
            {
                return value;
            }

            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }
    }
}
