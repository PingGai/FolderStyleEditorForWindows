using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FolderStyleEditorForWindows.Services;
using FolderStyleEditorForWindows.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FolderStyleEditorForWindows.Views
{
    public partial class HomeView : UserControl
    {
        private readonly DispatcherTimer _folderWordGradientTimer;
        private LinearGradientBrush? _folderWordAnimatedBrush;
        private readonly double[] _folderWordGradientBaseOffsets = { 0, 0.25, 0.5, 0.75, 1, 1.25, 1.5, 1.75, 2 };
        private double _folderWordGradientShift;

        public HomeView()
        {
            InitializeComponent();

            var dropZone = this.FindControl<Border>("dropZone");
            if (dropZone != null)
            {
                dropZone.PointerPressed += DropZone_PointerPressed;
            }
            var aboutInfoButton = this.FindControl<Button>("btnAboutInfo");
            if (aboutInfoButton != null)
            {
                aboutInfoButton.Click += BtnAboutInfo_Click;
            }

            var aboutInfoIcon = this.FindControl<Avalonia.Svg.Skia.Svg>("AboutInfoIcon");
            if (aboutInfoIcon != null)
            {
                aboutInfoIcon.Path = ConfigManager.Config.AppInfo.HelpIcon;
            }

            var historyList = this.FindControl<ListBox>("historyList");
            if (historyList != null)
            {
                historyList.SelectionMode = SelectionMode.Single;
                historyList.SelectionChanged += (s, e) => historyList.SelectedItem = null;
            }

            _folderWordGradientTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            _folderWordGradientTimer.Tick += FolderWordGradientTimer_Tick;

            if (Resources.TryGetResource("FolderWordAnimatedBrush", null, out var brushResource) &&
                brushResource is LinearGradientBrush brush)
            {
                _folderWordAnimatedBrush = brush;
                _folderWordGradientTimer.Start();
            }
        }

        private void FolderWordGradientTimer_Tick(object? sender, EventArgs e)
        {
            if (_folderWordAnimatedBrush == null || _folderWordAnimatedBrush.GradientStops.Count != _folderWordGradientBaseOffsets.Length)
            {
                return;
            }

            _folderWordGradientShift -= 0.012;
            if (_folderWordGradientShift <= -1)
            {
                _folderWordGradientShift = 0;
            }

            for (var i = 0; i < _folderWordGradientBaseOffsets.Length; i++)
            {
                _folderWordAnimatedBrush.GradientStops[i].Offset = _folderWordGradientBaseOffsets[i] + _folderWordGradientShift;
            }
        }

        private void HomeView_DragOver(object? sender, DragEventArgs e)
        {
            e.DragEffects = DragDropEffects.None;

            if (e.Data.GetFiles() is { } files && files.Any())
            {
                var firstItemPath = files.First().Path.LocalPath;
                if (IsSupportedDropItem(firstItemPath))
                {
                    e.DragEffects = DragDropEffects.Copy;
                }
            }

            e.Handled = true;
        }

        private void HomeView_Drop(object? sender, DragEventArgs e)
        {
            if (e.Data.GetFiles() is { } files && files.Any())
            {
                var firstItemPath = files.First().Path.LocalPath;
                if (string.IsNullOrEmpty(firstItemPath))
                {
                    return;
                }

                string folderPath;
                string? iconSourcePath = null;

                if (Directory.Exists(firstItemPath))
                {
                    folderPath = firstItemPath;
                }
                else if (File.Exists(firstItemPath) && IsSupportedDropItem(firstItemPath))
                {
                    folderPath = Path.GetDirectoryName(firstItemPath) ?? string.Empty;
                    iconSourcePath = firstItemPath;
                }
                else
                {
                    return;
                }

                if (!string.IsNullOrEmpty(folderPath) && VisualRoot is MainWindow mainWindow)
                {
                    mainWindow.GoToEditView(folderPath, iconSourcePath);
                }
            }

            e.Handled = true;
        }

        private bool IsSupportedDropItem(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            if (Directory.Exists(path))
            {
                return true;
            }

            if (File.Exists(path))
            {
                var extension = Path.GetExtension(path).ToLowerInvariant();
                switch (extension)
                {
                    case ".ico":
                        return true;
                    case ".exe":
                    case ".dll":
                        return ShellHelper.HasIcons(path);
                    default:
                        return false;
                }
            }

            return false;
        }

        private async void DropZone_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source is Control source && source.FindAncestorOfType<Button>() != null)
            {
                return;
            }

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is not { } storageProvider)
            {
                return;
            }

            var folder = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = LocalizationManager.Instance["Home_FolderPicker_Title"],
                AllowMultiple = false
            });

            if (folder.Count > 0)
            {
                var folderPath = folder[0].Path.LocalPath;
                if (DataContext is MainViewModel vm)
                {
                    vm.StartEditSession(folderPath);
                }
            }
        }

        private async void BtnAboutInfo_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var dialogService = App.Services?.GetRequiredService<InterruptDialogService>();
            if (dialogService == null)
            {
                return;
            }

            await dialogService.ShowSingleActionAsync(
                LocalizationManager.Instance["Home_AboutDialog_Title"],
                LocalizationManager.Instance["Home_AboutDialog_Content"],
                LocalizationManager.Instance["Dialog_Primary_Acknowledge"],
                LocalizationManager.Instance["Home_AboutDialog_SectionTitle"]);
        }

    }
}
