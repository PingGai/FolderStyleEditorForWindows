using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Platform.Storage;
using Avalonia.Controls.Shapes;
using Avalonia.Rendering;
using Avalonia.Styling;
using Avalonia.VisualTree;
using System;
using System.Diagnostics;
using FolderStyleEditorForWindows.Services;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using FolderStyleEditorForWindows.ViewModels;
using FolderStyleEditorForWindows.Views;

namespace FolderStyleEditorForWindows
{
    public partial class MainWindow : Window
    {
        private HomeView? _homeView;
        private EditView? _editView;
        private Border? _baseLayer;
        private Border? _flowLayer;
        private Grid? _cardsLayer;
        private MainViewModel _viewModel;
        private EditSessionManager _sessionManager;
        private Popup? _languagePopup;
        private readonly DispatcherTimer _doubleClickTimer;
        private int _clickCount;
        
        private readonly FrameLimiter _limiter = new(60);
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private bool _isAnimating = true;

        [SupportedOSPlatform("windows")]
        public MainWindow()
        {
// #if DEBUG
//             RendererDiagnostics.DebugOverlays =
//                 RendererDebugOverlays.Fps | RendererDebugOverlays.DirtyRects;
// #endif
            InitializeComponent();

            _viewModel = App.Services!.GetRequiredService<MainViewModel>();
            _sessionManager = new EditSessionManager(_viewModel);
            _viewModel.NavigateToEditView = (folderPath, iconSourcePath) => GoToEditView(folderPath, iconSourcePath);
            this.DataContext = _viewModel;

            _doubleClickTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(ConfigManager.Config.Features.Features.PinDoubleClickThreshold)
            };
            _doubleClickTimer.Tick += DoubleClickTimer_Tick;
            
            _homeView = this.FindControl<HomeView>("HomeView");
            _editView = this.FindControl<EditView>("EditView");
            _languagePopup = this.FindControl<Popup>("LanguagePopup");

            _baseLayer = this.FindControl<Border>("BaseLayer");
            _flowLayer = this.FindControl<Border>("FlowLayer");
            _cardsLayer = this.FindControl<Grid>("CardsLayer");

            var dragDropIndicator = this.FindControl<Rectangle>("DragDropIndicator");
            if (dragDropIndicator != null)
            {
                dragDropIndicator.StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 4, 4 };
            }

            // MainWindow now handles all drag-drop logic globally.
            this.AddHandler(DragDrop.DragEnterEvent, DragAndDropTarget_DragEnter);
            this.AddHandler(DragDrop.DragOverEvent, DragAndDropTarget_DragOver);
            this.AddHandler(DragDrop.DragLeaveEvent, DragAndDropTarget_DragLeave);
            this.AddHandler(DragDrop.DropEvent, DragAndDropTarget_Drop);
            
            this.Loaded += (s, e) => StartFlowLoop();
        }
        
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            var source = e.Source as Control;

            // Check if the click is on an interactive control that should retain focus.
            var isInteractiveControl = source?.FindAncestorOfType<TextBox>() != null ||
                                       source?.FindAncestorOfType<Button>() != null ||
                                       source?.FindAncestorOfType<Popup>() != null;

            if (!isInteractiveControl)
            {
                // If not an interactive control, clear the focus.
                // FocusManager.Instance is obsolete. Get it from the TopLevel.
                var topLevel = TopLevel.GetTopLevel(this);
                topLevel?.FocusManager?.ClearFocus();
                
                // Handle double-click to pin
                _clickCount++;
                var hoverIconService = App.Services!.GetRequiredService<HoverIconService>();

                if (_clickCount == 1)
                {
                    _doubleClickTimer.Start();
                    hoverIconService.ShowPinIcon("Waiting");
                }
                else if (_clickCount == 2)
                {
                    _doubleClickTimer.Stop();
                    _clickCount = 0;
                    
                    this.Topmost = !this.Topmost;
                    var toastService = App.Services!.GetRequiredService<IToastService>();
                    var message = this.Topmost ? LocalizationManager.Instance["Toast_WindowPinned"] : LocalizationManager.Instance["Toast_WindowUnpinned"];
                    toastService.Show(message, new SolidColorBrush(Color.Parse("#EBB762")));
                    hoverIconService.ShowPinIcon("Success");
                }
            }
            
            // Check if the window should be dragged.
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                // Only start dragging if the click was not on an interactive control.
                // This prevents dragging when clicking buttons, etc.
                if (!isInteractiveControl)
                {
                    BeginMoveDrag(e);
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.F2 && _editView?.IsVisible == true)
            {
                var aliasInput = _editView.FindControl<TextBox>("aliasInput");
                if (aliasInput != null)
                {
                    aliasInput.Focus();
                    aliasInput.SelectAll();
                    e.Handled = true;
                }
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            var hoverIconService = App.Services!.GetRequiredService<HoverIconService>();
            if (!_viewModel.IsDragOver)
            {
                hoverIconService.ShowPinIcon();
                hoverIconService.UpdatePosition(e.GetPosition(this));
            }
        }

        private void MainWindow_PointerExited(object? sender, PointerEventArgs e)
        {
            var hoverIconService = App.Services!.GetRequiredService<HoverIconService>();
            hoverIconService.Hide();
        }

        private void DoubleClickTimer_Tick(object? sender, EventArgs e)
        {
            _doubleClickTimer.Stop();
            _clickCount = 0;
            var hoverIconService = App.Services!.GetRequiredService<HoverIconService>();
            hoverIconService.ShowPinIcon("Ready");
        }

        private void DragAndDropTarget_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data.Contains(DataFormats.Files))
            {
                _viewModel.IsDragOver = true;
                e.DragEffects = DragDropEffects.Link;
                var hoverIconService = App.Services!.GetRequiredService<HoverIconService>();
                hoverIconService.ShowFileIcon(e.Data, _viewModel.FolderPath);
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void DragAndDropTarget_DragOver(object? sender, DragEventArgs e)
        {
            var hoverIconService = App.Services!.GetRequiredService<HoverIconService>();
            hoverIconService.UpdatePosition(e.GetPosition(this));

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
            var hoverIconService = App.Services!.GetRequiredService<HoverIconService>();
            hoverIconService.Hide();
            e.Handled = true;
        }

        private void DragAndDropTarget_Drop(object? sender, DragEventArgs e)
        {
            _viewModel.IsDragOver = false;
            var hoverIconService = App.Services!.GetRequiredService<HoverIconService>();
            hoverIconService.Hide();
            e.Handled = true;

            if (e.Data.GetFiles()?.FirstOrDefault() is not { } firstItem) return;
            
            var path = firstItem.Path.LocalPath;
            if (string.IsNullOrEmpty(path)) return;

            string folderPath;
            string? iconSourcePath = null;

            if (Directory.Exists(path))
            {
                folderPath = path;
            }
            else if (File.Exists(path))
            {
                // If a file is dropped, just update the icon path of the current session
                _viewModel.IconPath = path;
                return; // End the operation here
            }
            else
            {
                return;
            }

            if (!string.IsNullOrEmpty(folderPath))
            {
                _viewModel.StartEditSession(folderPath, iconSourcePath);
            }
        }

        [SupportedOSPlatform("windows")]
        public void GoToEditView(string folderPath, string? iconSourcePath)
        {
            if (_homeView == null) return;

            if (_editView == null)
            {
                _editView = new EditView();
                _cardsLayer?.Children.Add(_editView);
            }
            
            _isAnimating = false;
            
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

            _isAnimating = true;
            
            _editView.ZIndex = 0;
            _homeView.ZIndex = 1;

            _editView.IsVisible = false;
            
            // --- Final Fix ---
            if (_editView is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _cardsLayer?.Children.Remove(_editView);
            _editView = null;
            // ---

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
        
        private void PinButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            this.Topmost = !this.Topmost;
            var toastService = App.Services!.GetRequiredService<IToastService>();
            var message = this.Topmost ? LocalizationManager.Instance["Toast_WindowPinned"] : LocalizationManager.Instance["Toast_WindowUnpinned"];
            toastService.Show(message, new SolidColorBrush(Color.Parse("#EBB762")));
            var hoverIconService = App.Services!.GetRequiredService<HoverIconService>();
            hoverIconService.ShowPinIcon("Success");
        }
        
        /// <summary>
        /// A/B testing method to isolate performance-heavy layers.
        /// Call this from the debugger's immediate window, e.g., ShowOnly("flow")
        /// </summary>
        /// <param name="layer">"base", "cards", "blur", "flow", "blur + flow", or "all"</param>
        void ShowOnly(string layer)
        {
            // Ensure base layer is always visible for context, unless specifically testing "base" only
            if (_baseLayer != null) _baseLayer.IsVisible = true;

            // Hide all optional layers by default
            if (_flowLayer != null) _flowLayer.IsVisible = false;
            if (_cardsLayer != null) _cardsLayer.IsVisible = false;
            this.TransparencyLevelHint = new[] { WindowTransparencyLevel.None };

            switch (layer.ToLower())
            {
                case "base":
                    // Only the base layer is visible
                    if (_cardsLayer != null) _cardsLayer.IsVisible = false;
                    if (_flowLayer != null) _flowLayer.IsVisible = false;
                    break;
                
                case "cards":
                    if (_cardsLayer != null) _cardsLayer.IsVisible = true;
                    break;

                case "blur":
                    this.TransparencyLevelHint = new[] { WindowTransparencyLevel.AcrylicBlur };
                    break;

                case "flow":
                    if (_flowLayer != null) _flowLayer.IsVisible = true;
                    break;

                case "blur + flow":
                    this.TransparencyLevelHint = new[] { WindowTransparencyLevel.AcrylicBlur };
                    if (_flowLayer != null) _flowLayer.IsVisible = true;
                    break;

                case "all":
                default:
                    if (_flowLayer != null) _flowLayer.IsVisible = true;
                    if (_cardsLayer != null) _cardsLayer.IsVisible = true;
                    this.TransparencyLevelHint = new[] { WindowTransparencyLevel.AcrylicBlur };
                    break;
            }
        }
        
        void StartFlowLoop()
        {
            TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFrame);
        }

        void OnFrame(TimeSpan time)
        {
            if (_isAnimating && _limiter.Tick() && _flowLayer != null)
            {
                // This is a simplified C# version of the original XAML animation.
                // It cycles through colors and gradient points over 16 seconds.
                var totalSeconds = _stopwatch.Elapsed.TotalSeconds;
                var progress = (totalSeconds % 16) / 16.0; // Loop every 16 seconds

                // Alternate direction
                if (progress > 0.5)
                {
                    progress = 1.0 - progress;
                }
                progress *= 2.0;

                var startColor1 = Color.FromArgb(0x24, 0xFF, 0x74, 0x74);
                var endColor1 = Color.FromArgb(0x1F, 0x74, 0xA1, 0xFF);
                
                var startColor2 = Color.FromArgb(0x2E, 0x74, 0xFF, 0xC7);
                var endColor2 = Color.FromArgb(0x2E, 0xFF, 0xCB, 0x74);

                var newBrush = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(progress, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1 - progress, 1, RelativeUnit.Relative),
                    GradientStops = new GradientStops
                    {
                        new GradientStop(LerpColor(startColor1, startColor2, progress), 0),
                        new GradientStop(LerpColor(endColor1, endColor2, progress), 1)
                    }
                };

                _flowLayer.Background = newBrush;
            }
    
            // Request the next frame
            TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFrame);
        }

        private Color LerpColor(Color from, Color to, double progress)
        {
            var p = (float)progress;
            var a = (byte)(from.A + (to.A - from.A) * p);
            var r = (byte)(from.R + (to.R - from.R) * p);
            var g = (byte)(from.G + (to.G - from.G) * p);
            var b = (byte)(from.B + (to.B - from.B) * p);
            return Color.FromArgb(a, r, g, b);
        }
    }
    
    public sealed class FrameLimiter
    {
        private readonly double _targetMs;
        private long _last;
        public FrameLimiter(int fps = 60) => _targetMs = 1000.0 / fps;
        public bool Tick()
        {
            var now = Stopwatch.GetTimestamp();
            var ms = (now - _last) * 1000.0 / Stopwatch.Frequency;
            if (ms >= _targetMs) { _last = now; return true; }
            return false;
        }
    }
}