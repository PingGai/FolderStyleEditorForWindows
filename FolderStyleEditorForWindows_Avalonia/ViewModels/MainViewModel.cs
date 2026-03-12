using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Runtime;
using System.Runtime.InteropServices;
using Avalonia.Media;
using Avalonia.Threading;
using Newtonsoft.Json;
using FolderStyleEditorForWindows;
using FolderStyleEditorForWindows.Services;
using static FolderStyleEditorForWindows.Services.ConfigManager;

namespace FolderStyleEditorForWindows.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IconFinderService _iconFinderService;
        private readonly IToastService _toastService;
        private readonly HoverIconService _hoverIconService;
        private readonly InterruptDialogService _interruptDialogService;
        private readonly FolderStyleSaveCoordinator _saveCoordinator;
        private readonly ElevationSessionState _elevationSessionState;
        private List<string> _foundIconPaths = new List<string>();
        private int _currentIconIndex = -1;
        private bool _isScanningIcons = false;
        private bool _iconScanCompleted = false;
        private CancellationTokenSource? _iconScanCts;
        private string? _pendingIconPath = null;
        private readonly Dictionary<string, Stack<UndoEntry>> _undoStacks = new();
        private readonly Dictionary<string, Stack<UndoEntry>> _redoStacks = new();
        private readonly Dictionary<string, List<string>> _savedAliasHistory = new();
        private readonly Dictionary<string, int> _savedAliasHistoryCursor = new();
        private readonly ObservableCollection<AliasAutocompleteItemViewModel> _aliasAutocompleteCandidates = new();
        private static readonly string AliasHistoryFilePath = Path.Combine(ConfigManager.AppDataDirectory, "alias-history.json");
        private string _aliasAutocompleteSeed = string.Empty;
        private bool _suppressUndo = false;
        private bool _suppressAliasAutocomplete = false;
        private int _iconLoadingVersion;
        private CancellationTokenSource? _iconLoadCts;
        private readonly Dictionary<string, CachedIconPreviewSet> _iconPreviewCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Task> _iconPreviewWarmTasks = new(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _iconPreviewWarmGate = new(1, 1);
        private readonly SemaphoreSlim _historyIoGate = new(1, 1);
        private CancellationTokenSource? _memoryTrimCts;
        private long _lastMemoryTrimTicksUtc;
        private DebugOverlayViewModel? _debugOverlay;
        private const int IconPreviewCacheCapacity = 2;

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        private string _folderPath = "";
        public string FolderPath
        {
            get => _folderPath;
            [SupportedOSPlatform("windows")]
            set
            {
                // Always reload, even if the path is the same. This ensures that
                // re-entering the edit view from history correctly reloads folder settings.
                _folderPath = value;
                OnPropertyChanged();
                EnsureUndoStackExists(_folderPath);
                LoadFolderSettings();
            }
        }

        private string _alias = "";
        public string Alias
        {
            get => _alias;
            set
            {
                if (_alias == value) return;
                var oldValue = _alias;
                _alias = value;
                OnPropertyChanged();
                RecordUndoIfNeeded(UndoField.Alias, oldValue, _alias);
                if (!_suppressUndo)
                {
                    ResetSavedAliasCursor(FolderPath);
                }
                if (!_suppressAliasAutocomplete)
                {
                    if (IsAliasAutocompleteExpanded &&
                        !string.IsNullOrEmpty(_aliasAutocompletePreviewText) &&
                        !string.Equals(_aliasAutocompletePreviewText, _alias, StringComparison.Ordinal))
                    {
                        _aliasAutocompleteSeed = string.Empty;
                        IsAliasAutocompleteExpanded = false;
                        AliasAutocompleteSelectedIndex = -1;
                    }
                    UpdateAliasAutocomplete();
                }
                // When user types, remove the placeholder state
                if (IsAliasAsPlaceholder && !string.IsNullOrEmpty(value))
                {
                    if (Directory.Exists(FolderPath))
                    {
                        var directoryName = new DirectoryInfo(FolderPath).Name;
                        if (value != directoryName)
                        {
                            IsAliasAsPlaceholder = false;
                        }
                    }
                    else
                    {
                        IsAliasAsPlaceholder = false;
                    }
                }
            }
        }

        private string _iconPath = "";
        public string IconPath
        {
            get => _iconPath;
            set
            {
                // If the path is the same and the icon list is already populated, do nothing.
                // This forces a reload if the list is empty, fixing the issue where icons
                // wouldn't load when re-entering the edit view from history for the same item.
                if (_iconPath == value && Icons.Any())
                {
                    return;
                }

                var oldValue = _iconPath;
                _iconPath = value;
                OnPropertyChanged();
                QueueIconLoad(_iconPath);
                RecordUndoIfNeeded(UndoField.IconPath, oldValue, _iconPath);
            }
        }

        private bool _isDragOver;
        public bool IsDragOver
        {
            get => _isDragOver;
            set { if (_isDragOver == value) return; _isDragOver = value; OnPropertyChanged(); }
        }
        
        private Avalonia.Media.Geometry? _dragIconData;
        public Avalonia.Media.Geometry? DragIconData
        {
            get => _dragIconData;
            set { if (_dragIconData == value) return; _dragIconData = value; OnPropertyChanged(); }
        }

        private double _dragIconX;
        public double DragIconX
        {
            get => _dragIconX;
            set { if (_dragIconX == value) return; _dragIconX = value; OnPropertyChanged(); }
        }

        private double _dragIconY;
        public double DragIconY
        {
            get => _dragIconY;
            set { if (_dragIconY == value) return; _dragIconY = value; OnPropertyChanged(); }
        }

        private string _iconCounterText = "";
        public string IconCounterText
        {
            get => _iconCounterText;
            set { if (_iconCounterText == value) return; _iconCounterText = value; OnPropertyChanged(); }
        }

        private string _iconCounterNumerator = "0";
        public string IconCounterNumerator
        {
            get => _iconCounterNumerator;
            set { if (_iconCounterNumerator == value) return; _iconCounterNumerator = value; OnPropertyChanged(); }
        }

        private string _iconCounterDenominator = "???";
        public string IconCounterDenominator
        {
            get => _iconCounterDenominator;
            set { if (_iconCounterDenominator == value) return; _iconCounterDenominator = value; OnPropertyChanged(); }
        }

        private bool _isIconCounterVisible;
        public bool IsIconCounterVisible
        {
            get => _isIconCounterVisible;
            set { if (_isIconCounterVisible == value) return; _isIconCounterVisible = value; OnPropertyChanged(); }
        }
 
        private bool _isAliasAsPlaceholder;
        public bool IsAliasAsPlaceholder
        {
            get => _isAliasAsPlaceholder;
            set { if (_isAliasAsPlaceholder == value) return; _isAliasAsPlaceholder = value; OnPropertyChanged(); }
        }

        private bool _isElevationSessionActive;
        public bool IsElevationSessionActive
        {
            get => _isElevationSessionActive;
            set
            {
                if (_isElevationSessionActive == value) return;
                _isElevationSessionActive = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<AliasAutocompleteItemViewModel> AliasAutocompleteCandidates => _aliasAutocompleteCandidates;

        private string _aliasAutocompletePreviewText = string.Empty;
        public string AliasAutocompletePreviewText
        {
            get => _aliasAutocompletePreviewText;
            private set
            {
                if (_aliasAutocompletePreviewText == value) return;
                _aliasAutocompletePreviewText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AliasAutocompleteInlineSuffix));
                OnPropertyChanged(nameof(HasAliasAutocompleteInlineSuffix));
            }
        }

        public string AliasAutocompleteInlineSuffix =>
            !string.IsNullOrEmpty(AliasAutocompletePreviewText) &&
            !string.IsNullOrEmpty(Alias) &&
            AliasAutocompletePreviewText.StartsWith(Alias, StringComparison.OrdinalIgnoreCase) &&
            AliasAutocompletePreviewText.Length > Alias.Length
                ? AliasAutocompletePreviewText[Alias.Length..]
                : string.Empty;

        public bool HasAliasAutocompleteInlineSuffix => !string.IsNullOrEmpty(AliasAutocompleteInlineSuffix);

        private bool _isAliasAutocompleteExpanded;
        public bool IsAliasAutocompleteExpanded
        {
            get => _isAliasAutocompleteExpanded;
            private set
            {
                if (_isAliasAutocompleteExpanded == value) return;
                _isAliasAutocompleteExpanded = value;
                OnPropertyChanged();
            }
        }

        private int _aliasAutocompleteSelectedIndex = -1;
        public int AliasAutocompleteSelectedIndex
        {
            get => _aliasAutocompleteSelectedIndex;
            private set
            {
                if (_aliasAutocompleteSelectedIndex == value) return;
                _aliasAutocompleteSelectedIndex = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<IconViewModel> Icons { get; } = new();

        private IconViewModel? _selectedIcon;
        public IconViewModel? SelectedIcon
        {
            get => _selectedIcon;
            set
            {
                if (_selectedIcon == value) return;
                if (_selectedIcon != null)
                {
                    _selectedIcon.IsSelected = false;
                }
                
                _selectedIcon = value;

                if (_selectedIcon != null)
                {
                    _selectedIcon.IsSelected = true;
                    var newIconPath = $"{_selectedIcon.FilePath},{_selectedIcon.Index}";
                    if (IconPath != newIconPath)
                    {
                        var oldValue = _iconPath;
                        _iconPath = newIconPath;
                        OnPropertyChanged(nameof(IconPath));
                        RecordUndoIfNeeded(UndoField.IconPath, oldValue, _iconPath);
                    }
                }
                
                PreviewedIcon = _selectedIcon;
                OnPropertyChanged();
            }
        }

        private IconViewModel? _previewedIcon;
        public IconViewModel? PreviewedIcon
        {
            get => _previewedIcon;
            set
            {
                if (_previewedIcon == value) return;

                if (_previewedIcon != null)
                {
                    _previewedIcon.IsPreviewed = false;
                }

                _previewedIcon = value;

                if (_previewedIcon != null)
                {
                    _previewedIcon.IsPreviewed = true;
                }

                OnPropertyChanged();
            }
        }

        private bool _isLoadingIcons;
        public bool IsLoadingIcons
        {
            get => _isLoadingIcons;
            set
            {
                if (_isLoadingIcons == value) return;
                _isLoadingIcons = value;
                OnPropertyChanged();
            }
        }

        private bool _isLoadingIconsIndicatorVisible;
        public bool IsLoadingIconsIndicatorVisible
        {
            get => _isLoadingIconsIndicatorVisible;
            set
            {
                if (_isLoadingIconsIndicatorVisible == value) return;
                _isLoadingIconsIndicatorVisible = value;
                OnPropertyChanged();
            }
        }
        

        [SupportedOSPlatform("windows")]
        private void LoadFolderSettings()
        {
            var prevSuppress = _suppressUndo;
            _suppressUndo = true;
            CancelIconScan();
            _foundIconPaths.Clear();
            _currentIconIndex = -1;
            _iconScanCompleted = false;
            _isScanningIcons = false;
            IconCounterNumerator = "0";
            IconCounterDenominator = "???";
            IconCounterText = "";
            IsIconCounterVisible = false;

            try
            {
                var settings = new FolderSettings(FolderPath);
                Alias = settings.Alias;
                IconPath = settings.IconResource;

                if (!Directory.Exists(FolderPath))
                {
                    IsAliasAsPlaceholder = string.IsNullOrEmpty(Alias);
                    return;
                }
                
                if (string.IsNullOrEmpty(Alias))
                {
                    Alias = new DirectoryInfo(FolderPath).Name;
                    IsAliasAsPlaceholder = true;
                }
                else
                {
                    IsAliasAsPlaceholder = false;
                }

                if (string.IsNullOrEmpty(IconPath))
                {
                    // 使用默认的文件夹图标
                    IconPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shell32.dll,4");
                    _ = LoadIconsFromFileAsync(IconPath);
                }
            }
            finally
            {
                _suppressUndo = prevSuppress;
            }
        }

        [SupportedOSPlatform("windows")]
        public async void SaveFolderSettings()
        {
            var outcome = await _saveCoordinator.SaveAsync(new FolderStyleMutationRequest
            {
                FolderPath = FolderPath,
                Alias = Alias,
                IsAliasPlaceholder = IsAliasAsPlaceholder,
                IconPath = IconPath
            });

            if (outcome.Result.IsSuccess)
            {
                RecordSavedAlias(FolderPath, Alias);
                if (outcome.Result.HistoryShouldBeWritten)
                {
                    AddToHistory(FolderPath);
                }

                _toastService.Show(LocalizationManager.Instance["Toast_SaveSuccess"]);
            }
            else if (outcome.Result.Status != FolderStyleMutationStatus.CanceledByUser)
            {
                await _interruptDialogService.ShowFailureAsync(
                    LocalizationManager.Instance["Dialog_SaveFailed_Title"],
                    LocalizationManager.Instance["Dialog_SaveFailed_Headline"],
                    outcome.Result.Message,
                    outcome.Result.Details);
            }

            if (outcome.ShouldNavigateHome)
            {
                NavigateToHomeView?.Invoke();
            }

            if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (desktop.MainWindow is MainWindow mainWindow)
                {
                    var sessionManager = new EditSessionManager(this);
                    sessionManager.ClearSession();
                }
            }
        }

        public ObservableCollection<string> History { get; } = new();
        // Note: This path logic might need to be revisited if we want it to be truly portable.
        // For now, we assume it's in the same directory as the executable.
        private static readonly string HistoryFilePath = Path.Combine(ConfigManager.AppDataDirectory, "history.json");

        private void EnsureUndoStackExists(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return;
            if (!_undoStacks.ContainsKey(folderPath))
            {
                _undoStacks[folderPath] = new Stack<UndoEntry>();
            }
            if (!_redoStacks.ContainsKey(folderPath))
            {
                _redoStacks[folderPath] = new Stack<UndoEntry>();
            }
            if (!_savedAliasHistory.ContainsKey(folderPath))
            {
                _savedAliasHistory[folderPath] = new List<string>();
            }
            if (!_savedAliasHistoryCursor.ContainsKey(folderPath))
            {
                _savedAliasHistoryCursor[folderPath] = -1;
            }
        }

        private void RecordUndoIfNeeded(UndoField field, string oldValue, string newValue)
        {
            if (_suppressUndo) return;
            if (string.IsNullOrWhiteSpace(FolderPath)) return;
            if (oldValue == newValue) return;
            EnsureUndoStackExists(FolderPath);
            _undoStacks[FolderPath].Push(new UndoEntry
            {
                Field = field,
                OldValue = oldValue,
                NewValue = newValue
            });
            _redoStacks[FolderPath].Clear();
        }

        public void UndoLastChange()
        {
            var folder = FolderPath;
            if (string.IsNullOrWhiteSpace(folder)) return;
            if (!_undoStacks.TryGetValue(folder, out var stack) || stack.Count == 0) return;

            var entry = stack.Pop();
            _suppressUndo = true;
            try
            {
                switch (entry.Field)
                {
                    case UndoField.Alias:
                        Alias = entry.OldValue;
                        break;
                    case UndoField.IconPath:
                        IconPath = entry.OldValue;
                        break;
                }

                if (_redoStacks.TryGetValue(folder, out var redoStack))
                {
                    redoStack.Push(entry);
                }
            }
            finally
            {
                _suppressUndo = false;
            }
        }

        public void RedoLastChange()
        {
            var folder = FolderPath;
            if (string.IsNullOrWhiteSpace(folder)) return;
            if (!_redoStacks.TryGetValue(folder, out var stack) || stack.Count == 0) return;

            var entry = stack.Pop();
            _suppressUndo = true;
            try
            {
                switch (entry.Field)
                {
                    case UndoField.Alias:
                        Alias = entry.NewValue;
                        break;
                    case UndoField.IconPath:
                        IconPath = entry.NewValue;
                        break;
                }

                if (_undoStacks.TryGetValue(folder, out var undoStack))
                {
                    undoStack.Push(entry);
                }
            }
            finally
            {
                _suppressUndo = false;
            }
        }

        public void NavigateSavedAliasHistory(int direction)
        {
            if (IsAliasAutocompleteExpanded)
            {
                CycleAliasAutocompleteSelection(direction < 0 ? -1 : 1);
                return;
            }

            if (string.IsNullOrWhiteSpace(FolderPath) || !_savedAliasHistory.TryGetValue(FolderPath, out var history) || history.Count == 0)
            {
                return;
            }

            var cursor = _savedAliasHistoryCursor.TryGetValue(FolderPath, out var current) ? current : -1;

            if (direction < 0)
            {
                cursor = Math.Min(history.Count - 1, cursor + 1);
            }
            else if (direction > 0)
            {
                cursor = Math.Max(0, cursor - 1);
            }
            else
            {
                return;
            }

            _savedAliasHistoryCursor[FolderPath] = cursor;
            _suppressUndo = true;
            try
            {
                Alias = history[cursor];
            }
            finally
            {
                _suppressUndo = false;
            }

            UpdateAliasAutocomplete();
        }

        public ICommand SaveCommand { get; }
        public ICommand OpenFromHistoryCommand { get; }
        public ICommand ClearHistoryCommand { get; }
        public ICommand AutoGetIconCommand { get; }
        public ICommand LoadIconsCommand { get; }
        public ICommand GoHomeCommand { get; }
        public ICommand ResetIconCommand { get; }
        public ICommand ClearAllStylesCommand { get; }
        public Action<string, string?>? NavigateToEditView { get; set; }
        public Action? NavigateToHomeView { get; set; }
        
        public ObservableCollection<ToastViewModel> Toasts => ((ToastService)_toastService).Toasts;
        public HoverIconViewModel HoverIcon => _hoverIconService.ViewModel;
        public DebugOverlayViewModel? DebugOverlay
        {
            get => _debugOverlay;
            private set
            {
                if (ReferenceEquals(_debugOverlay, value))
                {
                    return;
                }

                _debugOverlay = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasDebugOverlay));
            }
        }
        public bool HasDebugOverlay => DebugOverlay?.IsEnabled == true;
        public InterruptDialogState InterruptDialog => _interruptDialogService.State;
        
        public async Task StartEditSessionAsync(string folderPath, string? iconSourcePath = null)
        {
            var access = await _saveCoordinator.PrepareAccessForFolderAsync(folderPath);
            if (!access.CanContinue)
            {
                if (access.ShouldNavigateHome)
                {
                    NavigateToHomeView?.Invoke();
                }

                return;
            }

            FolderPath = folderPath;
            RecordSavedAlias(folderPath, Alias);
            if (!string.IsNullOrEmpty(iconSourcePath))
            {
                IconPath = iconSourcePath;
            }
            UpdateAliasAutocomplete();
            NavigateToEditView?.Invoke(folderPath, iconSourcePath);
        }

        public async Task ToggleElevationSessionAsync()
        {
            if (_saveCoordinator.IsElevationSessionActive)
            {
                await _saveCoordinator.DisableElevationSessionAsync();
                _toastService.Show(LocalizationManager.Instance["Toast_ElevationExited"], new SolidColorBrush(Color.Parse("#D7A85B")));
                return;
            }

            await _saveCoordinator.EnsureElevationSessionAsync();
        }

        private void QueueIconLoad(string path)
        {
            _pendingIconPath = path;
            if (IsLoadingIcons)
            {
                return;
            }
            _ = StartNextIconLoadAsync();
        }

        private async Task StartNextIconLoadAsync()
        {
            var path = _pendingIconPath;
            _pendingIconPath = null;
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            if (LoadIconsCommand.CanExecute(path))
            {
                await LoadIconsFromFileAsync(path);
            }

            // 如果排队过程中又有新的请求，继续处理最新的
            if (_pendingIconPath != null && !IsLoadingIcons)
            {
                await StartNextIconLoadAsync();
            }
        }

        public void ClearIconPreview()
        {
            CancelIconScan();
            ClearDisplayedIcons();
            ClearIconPreviewCache();
        }
 
        [SupportedOSPlatform("windows")]
        public MainViewModel(IToastService toastService, HoverIconService hoverIconService, InterruptDialogService interruptDialogService, FolderStyleSaveCoordinator saveCoordinator, ElevationSessionState elevationSessionState)
        {
            _iconFinderService = new IconFinderService();
            _toastService = toastService;
            _hoverIconService = hoverIconService;
            _interruptDialogService = interruptDialogService;
            _saveCoordinator = saveCoordinator;
            _elevationSessionState = elevationSessionState;
            IsElevationSessionActive = _elevationSessionState.IsElevatedSessionActive;
            _elevationSessionState.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(ElevationSessionState.IsElevatedSessionActive))
                {
                    IsElevationSessionActive = _elevationSessionState.IsElevatedSessionActive;
                }
            };
            if (ConfigManager.Config.Debug.EnableOverlay)
            {
                DebugOverlay = new DebugOverlayViewModel(ConfigManager.Config, HoverIcon);
            }
            
            SaveCommand = new RelayCommand(SaveFolderSettings);
            OpenFromHistoryCommand = new RelayCommand<string?>(async path => await OpenFromHistoryAsync(path));
            ClearHistoryCommand = new RelayCommand(async () => await ConfirmClearHistoryAsync());
            AutoGetIconCommand = new RelayCommand(AutoGetIcon);
            LoadIconsCommand = new RelayCommand<string?>(async (filePath) => await LoadIconsFromFileAsync(filePath));
            GoHomeCommand = new RelayCommand(() => NavigateToHomeView?.Invoke());
            ResetIconCommand = new RelayCommand(ResetIcon);
            ClearAllStylesCommand = new RelayCommand(ClearAllStyles);
            _ = LoadPersistedDataAsync();
        }

        private async Task OpenFromHistoryAsync(string? path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
            await StartEditSessionAsync(path);
        }

        private async Task LoadPersistedDataAsync()
        {
            await LoadHistoryAsync();
            await LoadAliasHistoryAsync();
        }

        private async Task LoadHistoryAsync()
        {
            var readPath = ResolveReadablePath(
                HistoryFilePath,
                Path.Combine(Path.GetTempPath(), "WindowsFolderStyleEditor", "history.json"),
                Path.Combine(AppContext.BaseDirectory, "history.json"));
            if (readPath == null || !File.Exists(readPath))
            {
                return;
            }

            try
            {
                var json = await File.ReadAllTextAsync(readPath);
                var history = JsonConvert.DeserializeObject<ObservableCollection<string>>(json);
                if (history == null)
                {
                    return;
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    History.Clear();
                    foreach (var item in history)
                    {
                        History.Add(item);
                    }
                });

                if (!string.Equals(readPath, HistoryFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    await SaveHistoryAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LoadHistoryAsync failed: {ex.Message}");
            }
        }

        private async Task SaveHistoryAsync()
        {
            await _historyIoGate.WaitAsync();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(HistoryFilePath) ?? ConfigManager.AppDataDirectory);
                var json = JsonConvert.SerializeObject(History, Formatting.Indented);
                await File.WriteAllTextAsync(HistoryFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SaveHistoryAsync failed: {ex.Message}");
            }
            finally
            {
                _historyIoGate.Release();
            }
        }

        private async Task LoadAliasHistoryAsync()
        {
            var readPath = ResolveReadablePath(
                AliasHistoryFilePath,
                Path.Combine(Path.GetTempPath(), "WindowsFolderStyleEditor", "alias-history.json"),
                Path.Combine(AppContext.BaseDirectory, "alias-history.json"));
            if (readPath == null || !File.Exists(readPath))
            {
                return;
            }

            try
            {
                var json = await File.ReadAllTextAsync(readPath);
                var history = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json);
                if (history == null)
                {
                    return;
                }

                _savedAliasHistory.Clear();
                _savedAliasHistoryCursor.Clear();
                foreach (var item in history)
                {
                    _savedAliasHistory[item.Key] = item.Value
                        .Where(alias => !string.IsNullOrWhiteSpace(alias))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    _savedAliasHistoryCursor[item.Key] = 0;
                }

                if (!string.Equals(readPath, AliasHistoryFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    await SaveAliasHistoryAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LoadAliasHistoryAsync failed: {ex.Message}");
            }
        }

        private async Task SaveAliasHistoryAsync()
        {
            await _historyIoGate.WaitAsync();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AliasHistoryFilePath) ?? ConfigManager.AppDataDirectory);
                var json = JsonConvert.SerializeObject(_savedAliasHistory, Formatting.Indented);
                await File.WriteAllTextAsync(AliasHistoryFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SaveAliasHistoryAsync failed: {ex.Message}");
            }
            finally
            {
                _historyIoGate.Release();
            }
        }

        private void AddToHistory(string path)
        {
            if (History.Contains(path))
            {
                History.Move(History.IndexOf(path), 0);
            }
            else
            {
                History.Insert(0, path);
            }

            if (History.Count > 20)
            {
                History.RemoveAt(20);
            }
            
            _ = SaveHistoryAsync();
        }

        private static string? ResolveReadablePath(string preferredPath, params string[] legacyPaths)
        {
            if (File.Exists(preferredPath))
            {
                return preferredPath;
            }

            return legacyPaths
                .Where(File.Exists)
                .OrderByDescending(path => new FileInfo(path).LastWriteTimeUtc)
                .FirstOrDefault();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

       protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
       {
           PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
       }

       private void CancelIconScan()
       {
           _iconScanCts?.Cancel();
           _iconScanCts = null;
           _isScanningIcons = false;
       }

       private async Task ConfirmClearHistoryAsync()
       {
           if (History.Count == 0) return;

           var result = await _interruptDialogService.ShowDangerConfirmAsync(
               LocalizationManager.Instance["Dialog_ClearHistory_Title"],
               LocalizationManager.Instance["Dialog_ClearHistory_Content"],
               LocalizationManager.Instance["Dialog_Primary_Confirm"],
               LocalizationManager.Instance["Dialog_Secondary_Cancel"]);

           if (result.Result == InterruptDialogResult.Primary)
           {
               ClearHistoryInternal();
           }
       }

       private void ClearHistoryInternal()
       {
           History.Clear();
           if (File.Exists(HistoryFilePath))
           {
               File.Delete(HistoryFilePath);
           }
       }

        [SupportedOSPlatform("windows")]
        private void ResetIcon()
        {
            IconPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shell32.dll,4");
        }

        [SupportedOSPlatform("windows")]
        private void ClearAllStyles()
        {
            IconPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shell32.dll,4");
            if (Directory.Exists(FolderPath))
            {
                Alias = new DirectoryInfo(FolderPath).Name;
                IsAliasAsPlaceholder = true;
            }
            else
            {
                Alias = "";
            }
        }

       [SupportedOSPlatform("windows")]
       private async void AutoGetIcon()
       {
           try
           {
               if (_foundIconPaths.Any())
               {
                   if (_currentIconIndex < 0 && _foundIconPaths.Count > 0)
                   {
                       _currentIconIndex = 0;
                   }
                   MoveIconIndex(1, wrap: true);
                   return;
               }

               StartProgressiveIconScan();

               // 等待首个结果再跳转，避免空列表直接取模
               var cts = _iconScanCts;
               if (cts != null)
               {
                   await Task.Run(async () =>
                   {
                       var waitMs = 0;
                       while (!_iconScanCompleted && !_foundIconPaths.Any() && !cts.IsCancellationRequested && waitMs < 3000)
                       {
                           await Task.Delay(100);
                           waitMs += 100;
                       }
                   });
                   if (_foundIconPaths.Any())
                   {
                       _currentIconIndex = 0;
                       IconPath = _foundIconPaths[_currentIconIndex] + ",0";
                   }
                   else if (_iconScanCompleted)
                   {
                       UpdateIconCounterDisplay();
                   }
               }
           }
           catch (Exception ex)
           {
               Console.WriteLine($"AutoGetIcon failed: {ex.Message}");
           }
       }

       public void MoveIconIndex(int delta, bool wrap = false)
       {
           var count = _foundIconPaths.Count;
           if (count == 0)
           {
               UpdateIconCounterDisplay();
               return;
           }

           if (_currentIconIndex < 0)
           {
               _currentIconIndex = wrap ? 0 : Math.Clamp(delta >= 0 ? 0 : count - 1, 0, count - 1);
           }

           if (wrap)
           {
               _currentIconIndex = ((_currentIconIndex + delta) % count + count) % count;
           }
           else
           {
               _currentIconIndex = Math.Clamp(_currentIconIndex + delta, 0, count - 1);
           }
           IconPath = _foundIconPaths[_currentIconIndex] + ",0";
           UpdateIconCounterDisplay();
           PrefetchAdjacentAutoGetIcons();
       }

       public void JumpToIconIndex(int target)
       {
           if (!_foundIconPaths.Any())
           {
               UpdateIconCounterDisplay();
               return;
           }
           target = Math.Clamp(target - 1, 0, _foundIconPaths.Count - 1);
           _currentIconIndex = target;
           IconPath = _foundIconPaths[_currentIconIndex] + ",0";
           UpdateIconCounterDisplay();
           PrefetchAdjacentAutoGetIcons();
       }

       private void StartProgressiveIconScan()
       {
           _iconScanCts?.Cancel();
           _iconScanCts = new CancellationTokenSource();
           _isScanningIcons = true;
           _iconScanCompleted = false;
           _foundIconPaths.Clear();
           _currentIconIndex = -1;
           IsIconCounterVisible = true;
           IconCounterNumerator = "0";
           IconCounterDenominator = "???";
           IconCounterText = "0/???";

           var progress = new Progress<IconScanProgress>(p =>
           {
               _foundIconPaths = p.Found.Distinct().ToList();
               _iconScanCompleted = p.IsCompleted;
               if (_foundIconPaths.Any() && _currentIconIndex < 0)
               {
                   _currentIconIndex = 0;
                   IconPath = _foundIconPaths[_currentIconIndex] + ",0";
                   PrefetchAdjacentAutoGetIcons();
               }
               UpdateIconCounterDisplay();
           });

           _ = Task.Run(async () =>
           {
               try
               {
                   await _iconFinderService.FindIconsIncrementalAsync(FolderPath, progress, _iconScanCts.Token);
               }
               catch (OperationCanceledException) { }
               finally
               {
                   _isScanningIcons = false;
                   UpdateIconCounterDisplay();
               }
           });
       }

       private void UpdateIconCounterDisplay()
       {
           var numerator = _foundIconPaths.Any() && _currentIconIndex >= 0 ? _currentIconIndex + 1 : 0;
           IconCounterNumerator = numerator.ToString();
           IconCounterDenominator = _iconScanCompleted ? _foundIconPaths.Count.ToString() : "???";
           IconCounterText = $"{IconCounterNumerator}/{IconCounterDenominator}";
           IsIconCounterVisible = _isScanningIcons || _foundIconPaths.Any() || _iconScanCompleted;
       }
   
       [SupportedOSPlatform("windows")]
       private async Task LoadIconsFromFileAsync(string? filePath)
       {
           // 只允许串行加载，如果已有队列则由队列调用
           var parts = filePath?.Split(',') ?? Array.Empty<string>();
           var fileName = parts.Length > 0 ? parts[0].Trim() : "";
           int.TryParse(parts.Length > 1 ? parts[1].Trim() : "-1", out int selectedIndex);
 
           if (!string.IsNullOrEmpty(fileName))
           {
               fileName = Environment.ExpandEnvironmentVariables(fileName);
           }
           
           if (string.IsNullOrEmpty(fileName))
           {
               fileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shell32.dll");
               selectedIndex = 4; // 默认选中 shell32.dll 的常用文件夹图标
           }
 
           // 如果是相对路径，则将其转换为绝对路径
           if (!Path.IsPathRooted(fileName) && !string.IsNullOrEmpty(FolderPath))
           {
               fileName = Path.GetFullPath(Path.Combine(FolderPath, fileName));
           }
  
           if (!File.Exists(fileName))
           {
               ClearDisplayedIcons();
               return;
           }

           selectedIndex = ResolveSelectedIconIndex(fileName, selectedIndex);

           if (_iconPreviewCache.TryGetValue(fileName, out var cachedSet))
           {
               cachedSet.Touch();
               DisplayCachedIcons(cachedSet, selectedIndex);
               PrefetchAdjacentAutoGetIcons();
               return;
           }

           if (_iconPreviewWarmTasks.TryGetValue(fileName, out var warmTask))
           {
               IsLoadingIcons = true;
               IsLoadingIconsIndicatorVisible = false;
               try
               {
                   await warmTask;
               }
               finally
               {
                   IsLoadingIcons = false;
                   IsLoadingIconsIndicatorVisible = false;
               }

               if (_iconPreviewCache.TryGetValue(fileName, out cachedSet))
               {
                   cachedSet.Touch();
                   DisplayCachedIcons(cachedSet, selectedIndex);
                   PrefetchAdjacentAutoGetIcons();
                   return;
               }
           }
  
           var loadingVersion = ++_iconLoadingVersion;
           _iconLoadCts?.Cancel();
           _iconLoadCts = new CancellationTokenSource();
           var iconLoadToken = _iconLoadCts.Token;
           var loadedIcons = new List<IconViewModel>();
           IsLoadingIcons = true;
           IsLoadingIconsIndicatorVisible = false;
           _ = Task.Run(async () =>
           {
               await Task.Delay(250);
               await Dispatcher.UIThread.InvokeAsync(() =>
               {
                   if (IsLoadingIcons && loadingVersion == _iconLoadingVersion)
                   {
                       IsLoadingIconsIndicatorVisible = true;
                   }
               });
           });
  
           try
           {
               await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
               {
                   ClearDisplayedIcons();
               });

               var progress = new Progress<IconExtractionProgress>(update =>
               {
                    if (loadingVersion != _iconLoadingVersion)
                   {
                       foreach (var staleIcon in update.Batch)
                       {
                           staleIcon.Dispose();
                       }

                       return;
                   }

                   loadedIcons.AddRange(update.Batch);

                   Dispatcher.UIThread.Post(() =>
                   {
                       if (loadingVersion != _iconLoadingVersion)
                       {
                           foreach (var staleIcon in update.Batch)
                           {
                               staleIcon.Dispose();
                           }

                           return;
                       }

                       foreach (var icon in update.Batch)
                       {
                           Icons.Add(icon);
                       }

                       if (selectedIndex >= 0 && selectedIndex < Icons.Count && SelectedIcon != Icons[selectedIndex])
                       {
                           SelectedIcon = Icons[selectedIndex];
                           PreviewedIcon = Icons[selectedIndex];
                       }
                   }, DispatcherPriority.Background);
               });

               await _iconFinderService.ExtractIconsFromFileIncrementalAsync(fileName, progress, iconLoadToken);

               if (loadingVersion == _iconLoadingVersion)
               {
                   StoreIconPreviewCache(fileName, loadedIcons);
                   PrefetchAdjacentAutoGetIcons();
               }
           }
           catch (OperationCanceledException)
           {
           }
           catch (Exception ex)
           {
               Console.WriteLine($"Error loading icons: {ex.Message}");
           }
           finally
           {
               if (loadingVersion == _iconLoadingVersion)
               {
                   IsLoadingIcons = false;
                   IsLoadingIconsIndicatorVisible = false;
               }
               else
               {
                   foreach (var icon in loadedIcons)
                   {
                       icon.Dispose();
                   }
               }

               if (_pendingIconPath != null)
               {
                   await StartNextIconLoadAsync();
               }
           }
       }

       private void ClearDisplayedIcons()
       {
           PreviewedIcon = null;
           SelectedIcon = null;
           Icons.Clear();
       }

       private void DisplayCachedIcons(CachedIconPreviewSet cachedSet, int selectedIndex)
       {
           ClearDisplayedIcons();
           foreach (var icon in cachedSet.Icons)
           {
               Icons.Add(icon);
           }

           if (selectedIndex >= 0 && selectedIndex < Icons.Count)
           {
               SelectedIcon = Icons[selectedIndex];
               PreviewedIcon = Icons[selectedIndex];
           }
       }

       private void StoreIconPreviewCache(string fileName, List<IconViewModel> icons)
       {
           if (_iconPreviewCache.TryGetValue(fileName, out var existing))
           {
               existing.Touch();
               return;
           }

           _iconPreviewCache[fileName] = new CachedIconPreviewSet(fileName, icons);
           TrimIconPreviewCache();
       }

       private void ClearIconPreviewCache()
       {
           _iconLoadCts?.Cancel();
           _iconLoadCts = null;
           var hadCacheEntries = _iconPreviewCache.Count > 0;

           foreach (var cacheEntry in _iconPreviewCache.Values)
           {
               cacheEntry.Dispose();
           }

           _iconPreviewCache.Clear();
           _iconPreviewWarmTasks.Clear();

           if (hadCacheEntries)
           {
               ScheduleMemoryTrim();
           }
       }

       private void PrefetchAdjacentAutoGetIcons()
       {
           var nextPath = GetAdjacentAutoGetIconPath(1);

           if (!string.IsNullOrWhiteSpace(nextPath))
           {
               _ = WarmIconPreviewCacheAsync(nextPath);
           }
       }

       private string? GetAdjacentAutoGetIconPath(int offset)
       {
           if (!_foundIconPaths.Any() || _currentIconIndex < 0)
           {
               return null;
           }

           var count = _foundIconPaths.Count;
           var index = ((_currentIconIndex + offset) % count + count) % count;
           var path = _foundIconPaths[index];
           return File.Exists(path) ? path : null;
       }

       private async Task WarmIconPreviewCacheAsync(string fileName)
       {
           if (string.IsNullOrWhiteSpace(fileName) ||
               !File.Exists(fileName) ||
               _iconPreviewCache.ContainsKey(fileName))
           {
               return;
           }

           if (_iconPreviewWarmTasks.TryGetValue(fileName, out var existingTask))
           {
               await existingTask;
               return;
           }

           var warmTask = Task.Run(async () =>
           {
               await _iconPreviewWarmGate.WaitAsync();
               try
               {
                   if (_iconPreviewCache.ContainsKey(fileName))
                   {
                       return;
                   }

                   var icons = await _iconFinderService.ExtractIconsFromFileAsync(fileName);
                   StoreIconPreviewCache(fileName, icons);
               }
               finally
               {
                   _iconPreviewWarmGate.Release();
                   _iconPreviewWarmTasks.Remove(fileName);
               }
           });

           _iconPreviewWarmTasks[fileName] = warmTask;
           await warmTask;
       }

       private void TrimIconPreviewCache()
       {
           if (_iconPreviewCache.Count <= IconPreviewCacheCapacity)
           {
               return;
           }

           var evictedAny = false;

           var protectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
           var currentPath = GetCurrentIconPreviewFilePath();
           if (!string.IsNullOrWhiteSpace(currentPath))
           {
               protectedPaths.Add(currentPath);
           }

           var previousPath = GetAdjacentAutoGetIconPath(-1);
           if (!string.IsNullOrWhiteSpace(previousPath))
           {
               protectedPaths.Add(previousPath);
           }

           var nextPath = GetAdjacentAutoGetIconPath(1);
           if (!string.IsNullOrWhiteSpace(nextPath))
           {
               protectedPaths.Add(nextPath);
           }

           foreach (var evictKey in _iconPreviewCache
               .Where(pair => !protectedPaths.Contains(pair.Key))
               .OrderBy(pair => pair.Value.LastAccessUtc)
               .Select(pair => pair.Key)
               .ToList())
           {
               if (_iconPreviewCache.Count <= IconPreviewCacheCapacity)
               {
                   break;
               }

               if (_iconPreviewCache.Remove(evictKey, out var evictedSet))
               {
                   evictedSet.Dispose();
                   evictedAny = true;
               }
           }

           if (evictedAny)
           {
               ScheduleMemoryTrim(TimeSpan.FromSeconds(3));
           }
       }

       public void RequestIdleMemoryTrim()
       {
           if (GC.GetTotalMemory(false) < 96L * 1024L * 1024L)
           {
               return;
           }

           ScheduleMemoryTrim(
               delay: TimeSpan.FromSeconds(8),
               minInterval: TimeSpan.FromSeconds(90),
               allowWorkingSetTrim: false,
               forceAggressiveCollection: false);
       }

       private void ScheduleMemoryTrim(
           TimeSpan? delay = null,
           TimeSpan? minInterval = null,
           bool allowWorkingSetTrim = true,
           bool? forceAggressiveCollection = null)
       {
           _memoryTrimCts?.Cancel();
           var cts = new CancellationTokenSource();
           _memoryTrimCts = cts;
           var trimDelay = delay ?? TimeSpan.FromSeconds(2.5);

           _ = Task.Run(async () =>
           {
               try
               {
                   await Task.Delay(trimDelay, cts.Token);
               }
               catch (OperationCanceledException)
               {
                   return;
               }

               if (cts.IsCancellationRequested || IsLoadingIcons)
               {
                   return;
               }

               try
               {
                   var managedMemoryBytes = GC.GetTotalMemory(false);
                   var shouldUseAggressiveTrim = forceAggressiveCollection ?? (managedMemoryBytes >= 128L * 1024L * 1024L);

                   if (minInterval.HasValue)
                   {
                       var nowTicksUtc = DateTime.UtcNow.Ticks;
                       var lastTrimTicksUtc = Interlocked.Read(ref _lastMemoryTrimTicksUtc);
                       if (lastTrimTicksUtc > 0 && nowTicksUtc - lastTrimTicksUtc < minInterval.Value.Ticks)
                       {
                           return;
                       }

                       Interlocked.Exchange(ref _lastMemoryTrimTicksUtc, nowTicksUtc);
                   }

                   if (shouldUseAggressiveTrim)
                   {
                       GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                       GC.Collect(2, GCCollectionMode.Optimized, blocking: true, compacting: true);
                       GC.WaitForPendingFinalizers();
                       GC.Collect(2, GCCollectionMode.Optimized, blocking: true, compacting: true);
                   }
                   else
                   {
                       GC.Collect(1, GCCollectionMode.Optimized, blocking: false, compacting: false);
                   }

                   if (allowWorkingSetTrim && OperatingSystem.IsWindows())
                   {
                       using var process = System.Diagnostics.Process.GetCurrentProcess();
                       EmptyWorkingSet(process.Handle);
                   }
               }
               catch
               {
                   // Ignore best-effort memory trimming failures.
               }
           });
       }

       private string? GetCurrentIconPreviewFilePath()
       {
           var parts = IconPath?.Split(',') ?? Array.Empty<string>();
           var fileName = parts.Length > 0 ? parts[0].Trim() : string.Empty;
           if (string.IsNullOrWhiteSpace(fileName))
           {
               return null;
           }

           fileName = Environment.ExpandEnvironmentVariables(fileName);
           if (!Path.IsPathRooted(fileName) && !string.IsNullOrEmpty(FolderPath))
           {
               fileName = Path.GetFullPath(Path.Combine(FolderPath, fileName));
           }

           return fileName;
       }

       [SupportedOSPlatform("windows")]
       private static int ResolveSelectedIconIndex(string fileName, int selectedIndex)
       {
           if (selectedIndex >= 0)
           {
               return selectedIndex;
           }

           var extension = Path.GetExtension(fileName).ToLowerInvariant();
           if (extension != ".exe" && extension != ".dll")
           {
               return selectedIndex;
           }

           try
           {
               var groupNames = IconExtractor.ListIconGroups(fileName);
               if (!groupNames.Any())
               {
                   return selectedIndex;
               }

               var expectedGroupName = $"#{Math.Abs(selectedIndex)}";
               var resolvedIndex = groupNames.FindIndex(name =>
                   string.Equals(name, expectedGroupName, StringComparison.OrdinalIgnoreCase));

               return resolvedIndex >= 0 ? resolvedIndex : selectedIndex;
           }
           catch
           {
               return selectedIndex;
           }
       }
       
       [SupportedOSPlatform("windows")]
        public void RestoreDefaultAliasIfNeeded()
        {
            DismissAliasAutocomplete();
            if (string.IsNullOrEmpty(Alias))
            {
                if (Directory.Exists(FolderPath))
                {
                   Alias = new DirectoryInfo(FolderPath).Name;
                   IsAliasAsPlaceholder = true;
               }
           }
       }

       [SupportedOSPlatform("windows")]
       public void RestoreDefaultIconIfNeeded()
       {
           if (string.IsNullOrEmpty(IconPath))
           {
               ResetIcon();
           }
       }

       private enum UndoField
       {
           Alias,
           IconPath
       }

       private void RecordSavedAlias(string folderPath, string alias)
       {
           if (string.IsNullOrWhiteSpace(folderPath))
           {
               return;
           }

           EnsureUndoStackExists(folderPath);

           var list = _savedAliasHistory[folderPath];
           if (list.Count == 0 || !string.Equals(list[0], alias, StringComparison.Ordinal))
            {
                list.Insert(0, alias);
            }

            _savedAliasHistoryCursor[folderPath] = 0;
            _ = SaveAliasHistoryAsync();
            UpdateAliasAutocomplete();
        }

       private void ResetSavedAliasCursor(string folderPath)
       {
           if (string.IsNullOrWhiteSpace(folderPath))
           {
               return;
           }

            EnsureUndoStackExists(folderPath);
            _savedAliasHistoryCursor[folderPath] = 0;
        }

        public void DismissAliasAutocomplete()
        {
            _aliasAutocompleteSeed = string.Empty;
            IsAliasAutocompleteExpanded = false;
            AliasAutocompleteSelectedIndex = -1;
            AliasAutocompletePreviewText = string.Empty;
            RefreshAliasAutocompleteSelectionState();
            _aliasAutocompleteCandidates.Clear();
        }

        public bool TryAcceptOrExpandAliasAutocomplete()
        {
            UpdateAliasAutocomplete();
            if (_aliasAutocompleteCandidates.Count == 0)
            {
                return false;
            }

            if (_aliasAutocompleteCandidates.Count == 1)
            {
                CommitAliasAutocompleteCandidate(0);
                return true;
            }

            if (!IsAliasAutocompleteExpanded)
            {
                _aliasAutocompleteSeed = Alias ?? string.Empty;
                IsAliasAutocompleteExpanded = true;
                AliasAutocompleteSelectedIndex = 0;
                ApplyAliasAutocompletePreview(0);
                RefreshAliasAutocompletePreviewText();
                RefreshAliasAutocompleteSelectionState();
                return true;
            }

            return CycleAliasAutocompleteSelection(1);
        }

        public bool CycleAliasAutocompleteSelection(int delta)
        {
            if (!IsAliasAutocompleteExpanded || _aliasAutocompleteCandidates.Count == 0)
            {
                return false;
            }

            var nextIndex = AliasAutocompleteSelectedIndex < 0 ? 0 : AliasAutocompleteSelectedIndex + delta;
            if (nextIndex < 0)
            {
                nextIndex = _aliasAutocompleteCandidates.Count - 1;
            }
            else if (nextIndex >= _aliasAutocompleteCandidates.Count)
            {
                nextIndex = 0;
            }

            AliasAutocompleteSelectedIndex = nextIndex;
            ApplyAliasAutocompletePreview(nextIndex);
            RefreshAliasAutocompletePreviewText();
            RefreshAliasAutocompleteSelectionState();
            return true;
        }

        public void SelectAliasAutocompleteCandidate(AliasAutocompleteItemViewModel candidate)
        {
            var index = _aliasAutocompleteCandidates.IndexOf(candidate);
            if (index >= 0)
            {
                CommitAliasAutocompleteCandidate(index);
            }
        }

        private void UpdateAliasAutocomplete()
        {
            if (_suppressAliasAutocomplete || string.IsNullOrWhiteSpace(FolderPath))
            {
                DismissAliasAutocomplete();
                return;
            }

            EnsureUndoStackExists(FolderPath);
            var input = Alias ?? string.Empty;
            var searchInput = IsAliasAutocompleteExpanded && !string.IsNullOrWhiteSpace(_aliasAutocompleteSeed)
                ? _aliasAutocompleteSeed
                : input;
            if (string.IsNullOrWhiteSpace(searchInput))
            {
                DismissAliasAutocomplete();
                return;
            }

            var matches = _savedAliasHistory[FolderPath]
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Where(x => x.StartsWith(searchInput, StringComparison.OrdinalIgnoreCase))
                .Where(x => !string.Equals(x, searchInput, StringComparison.Ordinal))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            _aliasAutocompleteCandidates.Clear();
            foreach (var match in matches)
            {
                _aliasAutocompleteCandidates.Add(new AliasAutocompleteItemViewModel(match));
            }

            if (_aliasAutocompleteCandidates.Count == 0)
            {
                IsAliasAutocompleteExpanded = false;
                AliasAutocompleteSelectedIndex = -1;
                AliasAutocompletePreviewText = string.Empty;
                return;
            }

            if (IsAliasAutocompleteExpanded)
            {
                if (AliasAutocompleteSelectedIndex < 0 || AliasAutocompleteSelectedIndex >= _aliasAutocompleteCandidates.Count)
                {
                    AliasAutocompleteSelectedIndex = 0;
                }
            }
            else
            {
                AliasAutocompleteSelectedIndex = -1;
            }

            RefreshAliasAutocompletePreviewText();
            RefreshAliasAutocompleteSelectionState();
        }

        private void CommitAliasAutocompleteCandidate(int index)
        {
            if (index < 0 || index >= _aliasAutocompleteCandidates.Count)
            {
                return;
            }

            _suppressAliasAutocomplete = true;
            _suppressUndo = true;
            try
            {
                Alias = _aliasAutocompleteCandidates[index].Text;
            }
            finally
            {
                _suppressUndo = false;
                _suppressAliasAutocomplete = false;
            }

            DismissAliasAutocomplete();
        }

        private void RefreshAliasAutocompleteSelectionState()
        {
            for (var i = 0; i < _aliasAutocompleteCandidates.Count; i++)
            {
                _aliasAutocompleteCandidates[i].IsSelected = IsAliasAutocompleteExpanded && i == AliasAutocompleteSelectedIndex;
            }
        }

        private void RefreshAliasAutocompletePreviewText()
        {
            if (_aliasAutocompleteCandidates.Count == 0)
            {
                AliasAutocompletePreviewText = string.Empty;
                return;
            }

            if (IsAliasAutocompleteExpanded &&
                AliasAutocompleteSelectedIndex >= 0 &&
                AliasAutocompleteSelectedIndex < _aliasAutocompleteCandidates.Count)
            {
                AliasAutocompletePreviewText = _aliasAutocompleteCandidates[AliasAutocompleteSelectedIndex].Text;
                return;
            }

            AliasAutocompletePreviewText = _aliasAutocompleteCandidates[0].Text;
        }

        private void ApplyAliasAutocompletePreview(int index)
        {
            if (index < 0 || index >= _aliasAutocompleteCandidates.Count)
            {
                return;
            }

            _suppressAliasAutocomplete = true;
            _suppressUndo = true;
            try
            {
                Alias = _aliasAutocompleteCandidates[index].Text;
            }
            finally
            {
                _suppressUndo = false;
                _suppressAliasAutocomplete = false;
            }
        }

        private sealed class UndoEntry
        {
            public UndoField Field { get; set; }
            public string OldValue { get; set; } = string.Empty;
            public string NewValue { get; set; } = string.Empty;
        }

        public sealed class AliasAutocompleteItemViewModel : INotifyPropertyChanged
        {
            public AliasAutocompleteItemViewModel(string text)
            {
                Text = text;
            }

            public string Text { get; }

            private bool _isSelected;
            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if (_isSelected == value) return;
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
        }

        private sealed class CachedIconPreviewSet : IDisposable
        {
            public CachedIconPreviewSet(string filePath, List<IconViewModel> icons)
            {
                FilePath = filePath;
                Icons = icons;
                Touch();
            }

            public string FilePath { get; }
            public List<IconViewModel> Icons { get; }
            public DateTime LastAccessUtc { get; private set; }

            public void Touch()
            {
                LastAccessUtc = DateTime.UtcNow;
            }

            public void Dispose()
            {
                foreach (var icon in Icons)
                {
                    icon.Dispose();
                }
            }
        }
    }
}
