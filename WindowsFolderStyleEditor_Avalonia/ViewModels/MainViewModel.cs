using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Security;
using System.Threading.Tasks;
using System.Windows.Input;
using Newtonsoft.Json;
using WindowsFolderStyleEditor;
using WindowsFolderStyleEditor_Avalonia.Services;
using static WindowsFolderStyleEditor_Avalonia.Services.ConfigManager;

namespace WindowsFolderStyleEditor_Avalonia.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IconFinderService _iconFinderService;
        private List<string> _foundIconPaths = new List<string>();
        private int _currentIconIndex = -1;
        private bool _isFindingIcons = false;

        private string _folderPath = "";
        public string FolderPath
        {
            get => _folderPath;
            [SupportedOSPlatform("windows")]
            set
            {
                if (_folderPath == value) return;
                _folderPath = value;
                OnPropertyChanged();
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
                _alias = value;
                OnPropertyChanged();
                UpdateAliasPlaceholderState();
            }
        }

        private string _iconPath = "";
        public string IconPath
        {
            get => _iconPath;
            set
            {
                if (_iconPath == value) return;
                _iconPath = value;
                OnPropertyChanged();
                if (LoadIconsCommand.CanExecute(_iconPath))
                {
                    LoadIconsCommand.Execute(_iconPath);
                }
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
        
        private string _toastMessage = "";
        public string ToastMessage
        {
            get => _toastMessage;
            set { if (_toastMessage == value) return; _toastMessage = value; OnPropertyChanged(); }
        }

        private bool _isToastVisible;
        public bool IsToastVisible
        {
            get => _isToastVisible;
            set { if (_isToastVisible == value) return; _isToastVisible = value; OnPropertyChanged(); }
        }

        [SupportedOSPlatform("windows")]
        private void LoadFolderSettings()
        {
            _foundIconPaths.Clear();
            _currentIconIndex = -1;
            IsIconCounterVisible = false;
            IconCounterText = "";

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

        [SupportedOSPlatform("windows")]
        public async void SaveFolderSettings()
        {
            try
            {
                string finalIconPath = IconPath;

                // 如果用户只修改了别名而没有修改图标，保持原有的图标设置
                if (!string.IsNullOrEmpty(IconPath))
                {
                    finalIconPath = await PathHelper.ProcessIconPathAsync(FolderPath, IconPath);
                }
                else
                {
                    // 如果没有设置图标，使用默认的文件夹图标
                    finalIconPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shell32.dll,4");
                }

                DesktopIniHelper.WriteValue(FolderPath, "LocalizedResourceName", IsAliasAsPlaceholder ? "" : Alias);
                ShellHelper.SetFolderIcon(FolderPath, finalIconPath);
                AddToHistory(FolderPath);

                ToastMessage = $"✔ {LocalizationManager.Instance["Toast_SaveSuccess"]}";
                IsToastVisible = true;
                await Task.Delay(2000);
                IsToastVisible = false;
            }
            catch (UnauthorizedAccessException)
            {
                // TODO: Implement IPC with elevated helper process
                // For now, we can show a message or try to restart as admin.
                ToastMessage = $"❌ {LocalizationManager.Instance["Error_AdminRequired"]}";
                IsToastVisible = true;
                await Task.Delay(3000);
                IsToastVisible = false;
                // UacHelper.RestartAsAdmin();
            }
            catch (Exception ex) when (ex is IOException || ex is SecurityException)
            {
                ToastMessage = $"❌ {ex.Message}";
                IsToastVisible = true;
                await Task.Delay(3000);
                IsToastVisible = false;
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
        private static readonly string HistoryFilePath = ConfigManager.GetConfigFilePath("history.json");

        public ICommand SaveCommand { get; }
        public ICommand OpenFromHistoryCommand { get; }
        public ICommand ClearHistoryCommand { get; }
        public ICommand AutoGetIconCommand { get; }
        public ICommand LoadIconsCommand { get; }
        public ICommand GoHomeCommand { get; }
        public ICommand ResetIconCommand { get; }
        public ICommand ClearAllStylesCommand { get; }
        public Action<string>? NavigateToEditView { get; set; }
        public Action? NavigateToHomeView { get; set; }
 
        [SupportedOSPlatform("windows")]
        public MainViewModel()
        {
            _iconFinderService = new IconFinderService();
            SaveCommand = new RelayCommand(SaveFolderSettings);
            OpenFromHistoryCommand = new RelayCommand<string?>(OpenFromHistory);
            ClearHistoryCommand = new RelayCommand(ClearHistory);
            AutoGetIconCommand = new RelayCommand(AutoGetIcon, () => !_isFindingIcons);
            LoadIconsCommand = new RelayCommand<string?>(async (filePath) => await LoadIconsFromFileAsync(filePath));
            GoHomeCommand = new RelayCommand(() => NavigateToHomeView?.Invoke());
            ResetIconCommand = new RelayCommand(ResetIcon);
            ClearAllStylesCommand = new RelayCommand(ClearAllStyles);
            LoadHistory();
        }

        private void OpenFromHistory(string? path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
            NavigateToEditView?.Invoke(path);
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

       private void ClearHistory()
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
           if (_isFindingIcons) return;
 
           if (_foundIconPaths.Any())
           {
               _currentIconIndex = (_currentIconIndex + 1) % _foundIconPaths.Count;
               IconPath = _foundIconPaths[_currentIconIndex] + ",0";
               IconCounterText = $"{_currentIconIndex + 1}/{_foundIconPaths.Count}";
               return;
           }

           _isFindingIcons = true;
           ((RelayCommand)AutoGetIconCommand).RaiseCanExecuteChanged();

           try
           {
               _foundIconPaths = await _iconFinderService.FindIconsAsync(FolderPath);
               if (_foundIconPaths.Any())
               {
                   _currentIconIndex = 0;
                   IconPath = _foundIconPaths[_currentIconIndex] + ",0";
                   IconCounterText = $"{_currentIconIndex + 1}/{_foundIconPaths.Count}";
                   IsIconCounterVisible = true;
               }
               else
               {
                   IconCounterText = "0/0";
                   IsIconCounterVisible = true;
               }
           }
           finally
           {
               _isFindingIcons = false;
               ((RelayCommand)AutoGetIconCommand).RaiseCanExecuteChanged();
           }
       }
   
       private void UpdateAliasPlaceholderState()
       {
            if (!Directory.Exists(FolderPath)) return;

            var directoryName = new DirectoryInfo(FolderPath).Name;
            if (IsAliasAsPlaceholder && Alias != directoryName)
            {
                IsAliasAsPlaceholder = false;
            }
            else if (!IsAliasAsPlaceholder && (string.IsNullOrEmpty(Alias) || Alias == directoryName))
            {
                IsAliasAsPlaceholder = true;
                if (string.IsNullOrEmpty(Alias))
                {
                    Alias = directoryName;
                }
            }
       }

       [SupportedOSPlatform("windows")]
       private async Task LoadIconsFromFileAsync(string? filePath)
       {
           var parts = filePath?.Split(',') ?? Array.Empty<string>();
           var fileName = parts.Length > 0 ? parts[0].Trim() : "";
           int.TryParse(parts.Length > 1 ? parts[1].Trim() : "-1", out int selectedIndex);

           if (string.IsNullOrEmpty(fileName))
           {
               fileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shell32.dll");
               selectedIndex = 4; // 默认使用文件夹图标索引
           }

           // 如果是相对路径，则将其转换为绝对路径
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
           }
       }
       
    }
}