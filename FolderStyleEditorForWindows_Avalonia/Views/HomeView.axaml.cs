using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FolderStyleEditorForWindows.Services;
using FolderStyleEditorForWindows.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FolderStyleEditorForWindows.Views
{
    public partial class HomeView : UserControl
    {
        private const int GradientCycles = 3;
        private readonly DispatcherTimer _folderWordGradientTimer;
        private LinearGradientBrush? _folderWordAnimatedBrush;
        private readonly Color[] _defaultGradientColors =
        {
            Color.Parse("#8A6CFF"),
            Color.Parse("#65CCFF"),
            Color.Parse("#62E59B"),
            Color.Parse("#FFC263"),
            Color.Parse("#FF92B4")
        };
        private readonly Color[] _elevatedGradientColors =
        {
            Color.Parse("#FFB347"),
            Color.Parse("#FFD56A"),
            Color.Parse("#F3DF84"),
            Color.Parse("#E0A93F"),
            Color.Parse("#FFD07A")
        };
        private readonly ElevationSessionState? _elevationSessionState;
        private double _folderWordGradientPhase;
        private bool _isElevatedPalette;

        public HomeView()
        {
            InitializeComponent();

            var aboutInfoButton = this.FindControl<Button>("btnAboutInfo");
            if (aboutInfoButton != null)
            {
                aboutInfoButton.Click += BtnAboutInfo_Click;
            }
            var clickHintText = this.FindControl<TextBlock>("ClickHintText");
            if (clickHintText != null)
            {
                clickHintText.PointerPressed += ClickHintText_PointerPressed;
            }
            var folderWordButton = this.FindControl<Button>("FolderWordButton");
            if (folderWordButton != null)
            {
                folderWordButton.Click += FolderWordButton_Click;
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
                _elevationSessionState = App.Services?.GetRequiredService<ElevationSessionState>();
                if (_elevationSessionState != null)
                {
                    _elevationSessionState.PropertyChanged += ElevationSessionState_PropertyChanged;
                    ApplyFolderGradientPalette(_elevationSessionState.IsElevatedSessionActive);
                }
                _folderWordGradientTimer.Start();
            }
        }

        private void FolderWordGradientTimer_Tick(object? sender, EventArgs e)
        {
            if (_folderWordAnimatedBrush == null)
            {
                return;
            }

            _folderWordGradientPhase += 0.01;
            if (_folderWordGradientPhase >= 1)
            {
                _folderWordGradientPhase -= 1;
            }

            EnsureGradientPattern();
            UpdateGradientOffsets();
        }

        private void ElevationSessionState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ElevationSessionState.IsElevatedSessionActive) && _elevationSessionState != null)
            {
                ApplyFolderGradientPalette(_elevationSessionState.IsElevatedSessionActive);
            }
        }

        private void ApplyFolderGradientPalette(bool isElevated)
        {
            if (_folderWordAnimatedBrush == null)
            {
                return;
            }

            _isElevatedPalette = isElevated;
            EnsureGradientPattern();
            UpdateGradientOffsets();
        }

        private void EnsureGradientPattern()
        {
            if (_folderWordAnimatedBrush == null)
            {
                return;
            }

            var colors = _isElevatedPalette ? _elevatedGradientColors : _defaultGradientColors;
            var expectedStopCount = (colors.Length * GradientCycles) + 1;
            if (_folderWordAnimatedBrush.GradientStops.Count == expectedStopCount)
            {
                return;
            }

            _folderWordAnimatedBrush.GradientStops.Clear();
            for (var cycle = 0; cycle < GradientCycles; cycle++)
            {
                for (var colorIndex = 0; colorIndex < colors.Length; colorIndex++)
                {
                    _folderWordAnimatedBrush.GradientStops.Add(new GradientStop(
                        colors[colorIndex],
                        cycle + (colorIndex / (double)colors.Length)));
                }
            }

            _folderWordAnimatedBrush.GradientStops.Add(new GradientStop(colors[0], GradientCycles));
        }

        private void UpdateGradientOffsets()
        {
            if (_folderWordAnimatedBrush == null)
            {
                return;
            }

            var colors = _isElevatedPalette ? _elevatedGradientColors : _defaultGradientColors;
            if (colors.Length == 0)
            {
                return;
            }

            var stopIndex = 0;
            for (var cycle = 0; cycle < GradientCycles; cycle++)
            {
                for (var colorIndex = 0; colorIndex < colors.Length; colorIndex++)
                {
                    var offset = cycle + (colorIndex / (double)colors.Length) - _folderWordGradientPhase;
                    _folderWordAnimatedBrush.GradientStops[stopIndex].Color = colors[colorIndex];
                    _folderWordAnimatedBrush.GradientStops[stopIndex].Offset = offset;
                    stopIndex++;
                }
            }

            _folderWordAnimatedBrush.GradientStops[stopIndex].Color = colors[0];
            _folderWordAnimatedBrush.GradientStops[stopIndex].Offset = GradientCycles - _folderWordGradientPhase;
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

        private async void HomeView_Drop(object? sender, DragEventArgs e)
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

                if (!string.IsNullOrEmpty(folderPath) && DataContext is MainViewModel vm)
                {
                    await vm.StartEditSessionAsync(folderPath, iconSourcePath);
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

        private async Task OpenFolderPickerAsync()
        {
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
                    await vm.StartEditSessionAsync(folderPath);
                }
            }
        }

        private async void ClickHintText_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            await OpenFolderPickerAsync();
            e.Handled = true;
        }

        private async void FolderWordButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is not MainViewModel vm)
            {
                return;
            }

            await vm.ToggleElevationSessionAsync();
            e.Handled = true;
        }

        private async void BtnAboutInfo_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var dialogService = App.Services?.GetRequiredService<InterruptDialogService>();
            if (dialogService == null)
            {
                return;
            }

            await dialogService.ShowAboutDialogAsync();
        }

    }
}
