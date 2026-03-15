using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using FolderStyleEditorForWindows.ViewModels;
using Newtonsoft.Json;

namespace FolderStyleEditorForWindows
{
    public sealed class EditSessionManager : IDisposable
    {
        private readonly MainViewModel _viewModel;
        private CancellationTokenSource? _saveDebounceCts;
        private bool _disposed;
        private static readonly string TempFilePath = Path.Combine(Path.GetTempPath(), "wfse_session.json");

        public EditSessionManager(MainViewModel viewModel)
        {
            _viewModel = viewModel;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_disposed)
            {
                return;
            }

            if (e.PropertyName is not (nameof(MainViewModel.FolderPath) or nameof(MainViewModel.Alias) or nameof(MainViewModel.IconPath)))
            {
                return;
            }

            _ = ScheduleSaveStateAsync();
        }

        private async Task ScheduleSaveStateAsync()
        {
            _saveDebounceCts?.Cancel();
            var cts = new CancellationTokenSource();
            _saveDebounceCts = cts;

            try
            {
                await Task.Delay(180, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (cts.IsCancellationRequested || _disposed)
            {
                return;
            }

            await SaveStateToTempFileAsync().ConfigureAwait(false);
        }

        private async Task SaveStateToTempFileAsync()
        {
            if (string.IsNullOrWhiteSpace(_viewModel.FolderPath))
            {
                ClearPersistedSession();
                return;
            }

            var state = new SessionState
            {
                FolderPath = _viewModel.FolderPath,
                Alias = _viewModel.Alias,
                IconPath = _viewModel.IconPath
            };

            var json = JsonConvert.SerializeObject(state, Formatting.Indented);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(TempFilePath) ?? Path.GetTempPath());
                await File.WriteAllTextAsync(TempFilePath, json).ConfigureAwait(false);
            }
            catch (IOException)
            {
                // If the temp file is locked by another process, skip this save to keep UI alive.
            }
            catch (UnauthorizedAccessException)
            {
                // Skip silently; session restore is non-critical.
            }
        }

        [SupportedOSPlatform("windows")]
        public bool TryRestoreState()
        {
            if (!File.Exists(TempFilePath)) return false;

            try
            {
                var json = File.ReadAllText(TempFilePath);
                var state = JsonConvert.DeserializeObject<SessionState>(json);
                if (state == null) return false;

                if (string.IsNullOrWhiteSpace(state.FolderPath) || !Directory.Exists(state.FolderPath))
                {
                    ClearSession();
                    return false;
                }

                _viewModel.FolderPath = state.FolderPath;
                _viewModel.Alias = state.Alias ?? string.Empty;
                _viewModel.IconPath = state.IconPath ?? string.Empty;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void ClearSession()
        {
            ClearPersistedSession();
        }

        public static void ClearPersistedSession()
        {
            if (File.Exists(TempFilePath))
            {
                File.Delete(TempFilePath);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            _saveDebounceCts?.Cancel();
            _saveDebounceCts?.Dispose();
            _saveDebounceCts = null;
        }

        private sealed class SessionState
        {
            public string? FolderPath { get; set; }
            public string? Alias { get; set; }
            public string? IconPath { get; set; }
        }
    }
}
