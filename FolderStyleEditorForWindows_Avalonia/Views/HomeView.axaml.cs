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

        private void BtnMin_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (this.VisualRoot is Window window)
            {
                window.WindowState = WindowState.Minimized;
            }
        }

        private void BtnClose_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (this.VisualRoot is Window window)
            {
                window.Close();
            }
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
                    if (this.VisualRoot is MainWindow mainWindow)
                    {
                        mainWindow.GoToEditView(folderPath);
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