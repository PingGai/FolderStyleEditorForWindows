using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using System;
using System.IO;
using System.Linq;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Styling;
using Avalonia.Media;
using FolderStyleEditorForWindows.ViewModels;
using System.Threading.Tasks;
using System.Threading;

namespace FolderStyleEditorForWindows.Views
{
    public partial class HomeView : UserControl
    {
        private CancellationTokenSource? _animationCancellationToken;
        
        public HomeView()
        {
            InitializeComponent();

            var btnMin = this.FindControl<Button>("btnMin");
            if (btnMin != null)
            {
                btnMin.Click += BtnMin_Click;
            }

            var btnClose = this.FindControl<Button>("btnClose");
            if (btnClose != null)
            {
                btnClose.Click += BtnClose_Click;
            }
            
            var dropZone = this.FindControl<Border>("dropZone");
            if (dropZone != null)
            {
                dropZone.PointerPressed += DropZone_PointerPressed;
            }

            var clickHintText = this.FindControl<TextBlock>("ClickHintText");
            if (clickHintText != null)
            {
                clickHintText.PointerEntered += DropZone_PointerEntered;
                clickHintText.PointerExited += DropZone_PointerExited;
            }

           var historyList = this.FindControl<ListBox>("historyList");
           if (historyList != null)
           {
               historyList.SelectionMode = SelectionMode.Single;
               historyList.SelectionChanged += (s, e) => historyList.SelectedItem = null;
           }
       }

        private async void BtnMin_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (this.VisualRoot is MainWindow mainWindow)
            {
                await mainWindow.AnimateFadeOut();
                mainWindow.WindowState = WindowState.Minimized;
            }
        }

        private async void BtnClose_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (this.VisualRoot is MainWindow mainWindow)
            {
                await mainWindow.AnimateFadeOut();
                mainWindow.Close();
            }
        }
 
        private void HomeView_DragOver(object? sender, DragEventArgs e)
        {
            // Default to no drop
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
                if (string.IsNullOrEmpty(firstItemPath)) return;

                string folderPath;
                string? iconSourcePath = null;

                if (Directory.Exists(firstItemPath))
                {
                    // Case 1: A directory is dropped
                    folderPath = firstItemPath;
                }
                else if (File.Exists(firstItemPath) && IsSupportedDropItem(firstItemPath))
                {
                    // Case 2: A supported file is dropped
                    folderPath = Path.GetDirectoryName(firstItemPath) ?? "";
                    iconSourcePath = firstItemPath;
                }
                else
                {
                    // Not a supported item, do nothing
                    return;
                }

                if (!string.IsNullOrEmpty(folderPath) && this.VisualRoot is MainWindow mainWindow)
                {
                    // Navigate to Edit View with both folder path and icon source path
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

            // Case 1: It's a directory
            if (Directory.Exists(path))
            {
                return true;
            }

            // Case 2: It's a file
            if (File.Exists(path))
            {
                var extension = Path.GetExtension(path).ToLowerInvariant();
                switch (extension)
                {
                    case ".ico":
                        return true;
                    case ".exe":
                    case ".dll":
                        // For executables and libraries, check if they actually contain icons
                        return ShellHelper.HasIcons(path);
                    default:
                        return false;
                }
            }

            return false;
        }

        private async void DropZone_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is { } storageProvider)
            {
                var folder = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "选择文件夹",
                    AllowMultiple = false
                });

                if (folder.Count > 0)
                {
                    var folderPath = folder[0].Path.LocalPath;
                    if (this.DataContext is MainViewModel vm)
                    {
                        vm.StartEditSession(folderPath);
                    }
                }
            }
        }

       private async void DropZone_PointerEntered(object? sender, PointerEventArgs e)
       {
           var clickHintText = this.FindControl<TextBlock>("ClickHintText");

           if (clickHintText != null)
           {
               // 取消之前的动画
               _animationCancellationToken?.Cancel();
               _animationCancellationToken = new CancellationTokenSource();
               
               // 使用简单的动画实现平滑过渡
               await AnimateFontSize(clickHintText, 18, _animationCancellationToken.Token);
           }
       }
 
       private async void DropZone_PointerExited(object? sender, PointerEventArgs e)
       {
           var clickHintText = this.FindControl<TextBlock>("ClickHintText");

           if (clickHintText != null)
           {
               // 取消之前的动画
               _animationCancellationToken?.Cancel();
               _animationCancellationToken = new CancellationTokenSource();
               
               // 恢复原始字体大小
               await AnimateFontSize(clickHintText, 16, _animationCancellationToken.Token);
           }
       }

       private async Task AnimateFontSize(TextBlock textBlock, double targetSize, CancellationToken cancellationToken)
       {
           var currentSize = textBlock.FontSize;
           var duration = TimeSpan.FromMilliseconds(100);
           var steps = 10;
           var stepSize = (targetSize - currentSize) / steps;
           
           for (int i = 0; i < steps; i++)
           {
               // 检查是否取消
               if (cancellationToken.IsCancellationRequested)
                   return;
                   
               textBlock.FontSize = currentSize + (stepSize * (i + 1));
               
               try
               {
                   await Task.Delay(duration / steps, cancellationToken);
               }
               catch (TaskCanceledException)
               {
                   return;
               }
           }
           
           if (!cancellationToken.IsCancellationRequested)
           {
               textBlock.FontSize = targetSize;
           }
       }
       
    }
}