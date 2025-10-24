using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Platform.Storage;
using Avalonia.Controls.Shapes;
using Avalonia.Styling;
using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using FolderStyleEditerForWindows.ViewModels;
using FolderStyleEditerForWindows.Views;

namespace FolderStyleEditerForWindows
{
    public partial class MainWindow : Window
    {
        private HomeView? _homeView;
        private EditView? _editView;
        private MainViewModel _viewModel;
        private EditSessionManager _sessionManager;
        private Popup? _languagePopup;

        [SupportedOSPlatform("windows")]
        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new MainViewModel();
            _sessionManager = new EditSessionManager(_viewModel);
            _viewModel.NavigateToEditView = GoToEditView;
            this.DataContext = _viewModel;
            
            _homeView = this.FindControl<HomeView>("HomeView");
            _editView = this.FindControl<EditView>("EditView");
            _languagePopup = this.FindControl<Popup>("LanguagePopup");

            var dragDropIndicator = this.FindControl<Rectangle>("DragDropIndicator");
            if (dragDropIndicator != null)
            {
                dragDropIndicator.StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 4, 4 };
            }

            this.PointerPressed += (s, e) =>
            {
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                {
                    BeginMoveDrag(e);
                }
            };

            this.AddHandler(DragDrop.DragEnterEvent, DragAndDropTarget_DragEnter);
            this.AddHandler(DragDrop.DragOverEvent, DragAndDropTarget_DragOver);
            this.AddHandler(DragDrop.DragLeaveEvent, DragAndDropTarget_DragLeave);
            this.AddHandler(DragDrop.DropEvent, DragAndDropTarget_Drop);
        }
        
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
        }

        private void DragAndDropTarget_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data.Contains(DataFormats.Files))
            {
                _viewModel.IsDragOver = true;
                e.DragEffects = DragDropEffects.Link;
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void DragAndDropTarget_DragOver(object? sender, DragEventArgs e)
        {
            var canvas = this.FindControl<Canvas>("DragAdornerCanvas");
            if (canvas != null)
            {
                var point = e.GetPosition(canvas);
                _viewModel.DragIconX = point.X + 16;
                _viewModel.DragIconY = point.Y + 16;
            }

            var file = e.Data.GetFiles()?.FirstOrDefault();
            string iconKey;

            if (file is IStorageFolder)
            {
                iconKey = "FolderIcon";
                e.DragEffects = DragDropEffects.Link;
            }
            else if (file is IStorageFile storageFile)
            {
                var ext = System.IO.Path.GetExtension(storageFile.Name).ToLower();
                switch (ext)
                {
                    case ".ico":
                    case ".png":
                    case ".jpg":
                    case ".jpeg":
                    case ".bmp":
                        iconKey = "ImageIcon";
                        e.DragEffects = DragDropEffects.Link;
                        break;
                    case ".exe":
                    case ".dll":
                        iconKey = "ExecutableIcon";
                        e.DragEffects = DragDropEffects.Link;
                        break;
                    default:
                        iconKey = "UnsupportedIcon";
                        e.DragEffects = DragDropEffects.None;
                        break;
                }
            }
            else
            {
                iconKey = "UnsupportedIcon";
                e.DragEffects = DragDropEffects.None;
            }

            if (Application.Current?.TryFindResource(iconKey, out var resource) == true && resource is Avalonia.Media.Geometry geometry)
            {
                _viewModel.DragIconData = geometry;
            }
            
            e.Handled = true;
        }

        private void DragAndDropTarget_DragLeave(object? sender, DragEventArgs e)
        {
            _viewModel.IsDragOver = false;
            e.Handled = true;
        }

        private void DragAndDropTarget_Drop(object? sender, DragEventArgs e)
        {
            _viewModel.IsDragOver = false;

            if (e.Data.GetFiles()?.FirstOrDefault(item => item is IStorageFolder) is IStorageFolder folder)
            {
                var folderPath = folder.Path.LocalPath;
                if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                {
                    GoToEditView(folderPath);
                }
            }
            e.Handled = true;
        }

        [SupportedOSPlatform("windows")]
        public void GoToEditView(string folderPath)
        {
            if (_homeView == null || _editView == null) return;

            _viewModel.FolderPath = folderPath;
            
            _homeView.ZIndex = 0;
            _editView.ZIndex = 1;

            _homeView.IsVisible = false;
            _ = AnimateIn(_editView);
           
            var languageButton = this.FindControl<Button>("LanguageButton");
            if (languageButton != null) languageButton.IsVisible = false;
        }

        public void GoToHomeView()
        {
            if (_homeView == null || _editView == null) return;
            
            _editView.ZIndex = 0;
            _homeView.ZIndex = 1;

            _editView.IsVisible = false;
            _ = AnimateIn(_homeView);
           
            var languageButton = this.FindControl<Button>("LanguageButton");
            if (languageButton != null) languageButton.IsVisible = true;
        }

        private async Task AnimateIn(Control view)
        {
            view.IsVisible = true;
            view.RenderTransform = new Avalonia.Media.TranslateTransform();
            var animation = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(360),
                Easing = new CubicEaseOut(),
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0),
                        Setters =
                        {
                            new Setter(OpacityProperty, 0.0),
                            new Setter(Avalonia.Media.TranslateTransform.XProperty, 24.0)
                        }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1),
                        Setters =
                        {
                            new Setter(OpacityProperty, 1.0),
                            new Setter(Avalonia.Media.TranslateTransform.XProperty, 0.0)
                        }
                    }
                }
            };
            await animation.RunAsync(view, System.Threading.CancellationToken.None);
        }

        private async Task AnimateOut(Control view)
        {
            view.RenderTransform = new Avalonia.Media.TranslateTransform();
            var animation = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(320),
                Easing = new CubicEaseOut(),
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0),
                        Setters =
                        {
                            new Setter(OpacityProperty, 1.0),
                            new Setter(Avalonia.Media.TranslateTransform.XProperty, 0.0)
                        }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1),
                        Setters =
                        {
                            new Setter(OpacityProperty, 0.0),
                            new Setter(Avalonia.Media.TranslateTransform.XProperty, 24.0)
                        }
                    }
                }
            };
            await animation.RunAsync(view, System.Threading.CancellationToken.None);
            view.IsVisible = false;
        }
        
        private async void LanguageButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_languagePopup == null) return;

            _languagePopup.IsOpen = !_languagePopup.IsOpen;

            if (_languagePopup.IsOpen && _languagePopup.Child is { } popupContent)
            {
                popupContent.RenderTransform = new Avalonia.Media.TranslateTransform();
                var animation = new Animation
                {
                    Duration = TimeSpan.FromMilliseconds(300),
                    Easing = new CubicEaseOut(),
                    Children =
                    {
                        new KeyFrame
                        {
                            Cue = new Cue(0),
                            Setters =
                            {
                                new Setter(OpacityProperty, 0.0),
                                new Setter(Avalonia.Media.TranslateTransform.YProperty, 10.0)
                            }
                        },
                        new KeyFrame
                        {
                            Cue = new Cue(1),
                            Setters =
                            {
                                new Setter(OpacityProperty, 1.0),
                                new Setter(Avalonia.Media.TranslateTransform.YProperty, 0.0)
                            }
                        }
                    }
                };
                await animation.RunAsync(popupContent, System.Threading.CancellationToken.None);
            }
        }
    }
}