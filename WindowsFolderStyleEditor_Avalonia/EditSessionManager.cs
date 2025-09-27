using System;
using System.IO;
using System.Runtime.Versioning;
using Newtonsoft.Json;
using WindowsFolderStyleEditor_Avalonia.ViewModels;

namespace WindowsFolderStyleEditor_Avalonia
{
    public class EditSessionManager
    {
        private readonly MainViewModel _viewModel;
        private string _tempFilePath;

        public EditSessionManager(MainViewModel viewModel)
        {
            _viewModel = viewModel;
            _viewModel.PropertyChanged += (s, e) => SaveStateToTempFile();
            _tempFilePath = Path.Combine(Path.GetTempPath(), "wfse_session.json");
        }

        private void SaveStateToTempFile()
        {
            if (string.IsNullOrWhiteSpace(_viewModel.FolderPath))
            {
                if (File.Exists(_tempFilePath))
                {
                    File.Delete(_tempFilePath);
                }
                return;
            }

            var state = new SessionState
            {
                FolderPath = _viewModel.FolderPath,
                Alias = _viewModel.Alias,
                IconPath = _viewModel.IconPath
            };
            var json = JsonConvert.SerializeObject(state, Formatting.Indented);
            File.WriteAllText(_tempFilePath, json);
        }

        [SupportedOSPlatform("windows")]
        public bool TryRestoreState()
        {
            if (!File.Exists(_tempFilePath)) return false;

            try
            {
                var json = File.ReadAllText(_tempFilePath);
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
            if (File.Exists(_tempFilePath))
            {
                File.Delete(_tempFilePath);
            }
        }

        private sealed class SessionState
        {
            public string? FolderPath { get; set; }
            public string? Alias { get; set; }
            public string? IconPath { get; set; }
        }
    }
}
