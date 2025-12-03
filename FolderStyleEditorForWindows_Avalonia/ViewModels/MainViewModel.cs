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
using Avalonia.Media;
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
        private List<string> _foundIconPaths = new List<string>();
        private int _currentIconIndex = -1;
        private bool _isScanningIcons = false;
        private bool _iconScanCompleted = false;
        private CancellationTokenSource? _iconScanCts;
        private string? _pendingIconPath = null;
        private readonly Dictionary<string, Stack<UndoEntry>> _undoStacks = new();
        private bool _suppressUndo = false;

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
                        IconPath = newIconPath;
                    }
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
            try
            {
                DesktopIniHelper.WriteValue(FolderPath, "LocalizedResourceName", IsAliasAsPlaceholder ? "" : Alias);

                if (!string.IsNullOrEmpty(IconPath))
                {
                    var iconSettings = await PathHelper.ProcessIconPathAsync(FolderPath, IconPath);
                    
                    // Clear all possible icon-related keys first to avoid conflicts
                    DesktopIniHelper.WriteValue(FolderPath, "IconResource", null);
                    DesktopIniHelper.WriteValue(FolderPath, "IconFile", null);
                    DesktopIniHelper.WriteValue(FolderPath, "IconIndex", null);

                    string finalIconPathForRefresh = "";
                    foreach (var (key, value) in iconSettings)
                    {
                        DesktopIniHelper.WriteValue(FolderPath, key, value);
                        if (key == "IconResource") finalIconPathForRefresh = value;
                        if (key == "IconFile") finalIconPathForRefresh = value;
                    }
                    
                    ShellHelper.SetFolderIcon(FolderPath, finalIconPathForRefresh);
                }
                else
                {
                    // If no icon is set, clear all settings and refresh
                    DesktopIniHelper.WriteValue(FolderPath, "IconResource", null);
                    DesktopIniHelper.WriteValue(FolderPath, "IconFile", null);
                    DesktopIniHelper.WriteValue(FolderPath, "IconIndex", null);
                    ShellHelper.RemoveFolderIcon(FolderPath);
                }
                
                AddToHistory(FolderPath);

                _toastService.Show(LocalizationManager.Instance["Toast_SaveSuccess"]);
            }
            catch (UnauthorizedAccessException)
            {
                // TODO: Implement IPC with elevated helper process
                // For now, we can show a message or try to restart as admin.
                _toastService.Show("鉂?" + LocalizationManager.Instance["Error_AdminRequired"],
                    new SolidColorBrush(Color.Parse("#EBB762")));
                // UacHelper.RestartAsAdmin();
            }
            catch (Exception ex) when (ex is IOException || ex is SecurityException)
            {
                _toastService.Show($"鉂?{ex.Message}", new SolidColorBrush(Color.Parse("#EBB762")));
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
        private static readonly string HistoryFilePath = Path.Combine(AppContext.BaseDirectory, "history.json");

        private void EnsureUndoStackExists(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return;
            if (!_undoStacks.ContainsKey(folderPath))
            {
                _undoStacks[folderPath] = new Stack<UndoEntry>();
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
            }
            finally
            {
                _suppressUndo = false;
            }
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
        public DebugOverlayViewModel DebugOverlay { get; }
        public InterruptDialogState InterruptDialog => _interruptDialogService.State;
        
        public void StartEditSession(string folderPath, string? iconSourcePath = null)
        {
            FolderPath = folderPath;
            if (!string.IsNullOrEmpty(iconSourcePath))
            {
                IconPath = iconSourcePath;
            }
            NavigateToEditView?.Invoke(folderPath, iconSourcePath);
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
            foreach (var icon in Icons)
            {
                icon.Dispose();
            }
            Icons.Clear();
            SelectedIcon = null;
        }
 
        [SupportedOSPlatform("windows")]
        public MainViewModel(IToastService toastService, HoverIconService hoverIconService, InterruptDialogService interruptDialogService)
        {
            _iconFinderService = new IconFinderService();
            _toastService = toastService;
            _hoverIconService = hoverIconService;
            _interruptDialogService = interruptDialogService;
            DebugOverlay = new DebugOverlayViewModel(ConfigManager.Config, HoverIcon);
            
            SaveCommand = new RelayCommand(SaveFolderSettings);
            OpenFromHistoryCommand = new RelayCommand<string?>(OpenFromHistory);
            ClearHistoryCommand = new RelayCommand(async () => await ConfirmClearHistoryAsync());
            AutoGetIconCommand = new RelayCommand(AutoGetIcon);
            LoadIconsCommand = new RelayCommand<string?>(async (filePath) => await LoadIconsFromFileAsync(filePath));
            GoHomeCommand = new RelayCommand(() => NavigateToHomeView?.Invoke());
            ResetIconCommand = new RelayCommand(ResetIcon);
            ClearAllStylesCommand = new RelayCommand(ClearAllStyles);
            LoadHistory();
        }

        private void OpenFromHistory(string? path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
            StartEditSession(path);
        }

        private void LoadHistory()
        {
            if (!File.Exists(HistoryFilePath)) return;
            var json = File.ReadAllText(HistoryFilePath);
            var history = JsonConvert.DeserializeObject<ObservableCollection<string>>(json);
            if (history == null) return;
            History.Clear();
            foreach (var item in history)
            {
                History.Add(item);
            }
        }

        private void SaveHistory()
        {
            var json = JsonConvert.SerializeObject(History, Formatting.Indented);
            File.WriteAllText(HistoryFilePath, json);
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
            
            SaveHistory();
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

           var result = await _interruptDialogService.ShowAsync(new InterruptDialogOptions
           {
               Title = LocalizationManager.Instance["Dialog_ClearHistory_Title"],
               Content = LocalizationManager.Instance["Dialog_ClearHistory_Content"],
               PrimaryButtonText = LocalizationManager.Instance["Dialog_Primary_Confirm"],
               SecondaryButtonText = LocalizationManager.Instance["Dialog_Secondary_Cancel"]
           });

           if (result == InterruptDialogResult.Primary)
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
               selectedIndex = 4; // 榛樿浣跨敤鏂囦欢澶瑰浘鏍囩储寮?
           }
 
           // 濡傛灉鏄浉瀵硅矾寰勶紝鍒欏皢鍏惰浆鎹负缁濆璺緞
           if (!Path.IsPathRooted(fileName) && !string.IsNullOrEmpty(FolderPath))
           {
               fileName = Path.GetFullPath(Path.Combine(FolderPath, fileName));
           }
  
           if (!File.Exists(fileName))
           {
               Icons.Clear();
               return;
           }
  
           IsLoadingIcons = true;
  
           try
           {
                var newIcons = await Task.Run(() => _iconFinderService.ExtractIconsFromFileAsync(fileName));

               await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
               {
                   SelectedIcon = null;
                   Icons.Clear();
                   foreach (var icon in newIcons)
                   {
                       Icons.Add(icon);
                   }

                   if (selectedIndex >= 0 && selectedIndex < Icons.Count)
                   {
                       SelectedIcon = Icons[selectedIndex];
                   }
                   else
                   {
                       SelectedIcon = null;
                   }
               });
           }
           catch (Exception ex)
           {
               Console.WriteLine($"Error loading icons: {ex.Message}");
           }
           finally
           {
               IsLoadingIcons = false;
               if (_pendingIconPath != null)
               {
                   await StartNextIconLoadAsync();
               }
           }
       }
       
       [SupportedOSPlatform("windows")]
       public void RestoreDefaultAliasIfNeeded()
       {
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

        private sealed class UndoEntry
        {
            public UndoField Field { get; set; }
            public string OldValue { get; set; } = string.Empty;
            public string NewValue { get; set; } = string.Empty;
        }
    }
}





