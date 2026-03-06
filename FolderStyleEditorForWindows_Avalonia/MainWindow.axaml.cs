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
using Avalonia.Controls.Metadata;
using Avalonia.VisualTree;
using System;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using FolderStyleEditorForWindows.ViewModels;
using FolderStyleEditorForWindows.Views;
using FolderStyleEditorForWindows.Services;

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
        private Avalonia.Svg.Skia.Svg? _pinButtonIcon;
        private Button? _pinButton;
        private Button? _languageButton;
        private Popup? _pinToolTipPopup;
        private Popup? _langToolTipPopup;
        private TextBlock? _pinToolTipTextBlock;
        private TextBlock? _langToolTipTextBlock;
        private Border? _pinIconGlow;
        private readonly DispatcherTimer _pinGlowTimer;
        private double _pinGlowPhase;
        
        private readonly FrameLimiter _limiter = new(60);
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private bool _isAnimating = true;
        private bool _closingAnimating;
        private Control? _fadeRoot;
 
        [SupportedOSPlatform("windows")]
        public MainWindow()
        {
// #if DEBUG
//             RendererDiagnostics.DebugOverlays =
//                 RendererDebugOverlays.Fps | RendererDebugOverlays.DirtyRects;
// #endif
            InitializeComponent();
            
            // 鍚姩鍏堣涓?0锛岀瓑 OnOpened 鍐嶆笎鍏ワ紙鍚﹀垯浣犲彲鑳解€滅湅涓嶅埌鈥濓級
            Opacity = 0;
 
            _viewModel = App.Services!.GetRequiredService<MainViewModel>();
            _sessionManager = new EditSessionManager(_viewModel);
            _viewModel.NavigateToEditView = (folderPath, iconSourcePath) => GoToEditView(folderPath, iconSourcePath);
            this.DataContext = _viewModel;

            _doubleClickTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(ConfigManager.Config.Features.Features.PinDoubleClickThreshold)
            };
            _doubleClickTimer.Tick += DoubleClickTimer_Tick;

            _pinGlowTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            _pinGlowTimer.Tick += PinGlowTimer_Tick;
            
            _homeView = this.FindControl<HomeView>("HomeView");
            _editView = this.FindControl<EditView>("EditView");
            _languagePopup = this.FindControl<Popup>("LanguagePopup");
            _pinButtonIcon = this.FindControl<Avalonia.Svg.Skia.Svg>("PinButtonIcon");
            _pinButton = this.FindControl<Button>("PinButton");
            _languageButton = this.FindControl<Button>("LanguageButton");
            if (_pinButton != null)
            {
                _pinButton.TemplateApplied += PinButton_TemplateApplied;
            }

           _pinToolTipPopup = this.FindControl<Popup>("PinToolTipPopup");
           _langToolTipPopup = this.FindControl<Popup>("LangToolTipPopup");
           _pinToolTipTextBlock = this.FindControl<TextBlock>("PinToolTipTextBlock");
           _langToolTipTextBlock = this.FindControl<TextBlock>("LangToolTipTextBlock");

           LocalizationManager.Instance.PropertyChanged += (sender, args) =>
           {
               if (args.PropertyName == string.Empty) // Language changed
               {
                   if (_pinToolTipTextBlock != null)
                       _pinToolTipTextBlock.Text = LocalizationManager.Instance["Pin_Tip_DoubleClickHint"];
                   if (_langToolTipTextBlock != null)
                       _langToolTipTextBlock.Text = LocalizationManager.Instance["Language_Tip"];
               }
           };

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
            
            UpdatePinButtonIcon();
        }

        private void PinButton_TemplateApplied(object? sender, TemplateAppliedEventArgs e)
        {
            _pinIconGlow = e.NameScope.Find<Border>("PinIconGlow");
            if (_pinIconGlow != null && _pinIconGlow.RenderTransform is not ScaleTransform)
            {
                _pinIconGlow.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
                _pinIconGlow.RenderTransform = new ScaleTransform(1, 1);
            }

            UpdatePinGlowVisual();
        }

        private async void PinButton_PointerEntered(object? sender, PointerEventArgs e)
        {
            if (_pinToolTipPopup != null)
            {
                _pinToolTipPopup.PlacementTarget = _pinButton;
                _pinToolTipPopup.IsOpen = true;
                await Task.Delay(10); // Ensure popup is positioned before fading in
                _pinToolTipPopup.Opacity = 1;
            }
        }

        private async void PinButton_PointerExited(object? sender, PointerEventArgs e)
        {
            if (_pinToolTipPopup != null)
            {
                _pinToolTipPopup.Opacity = 0;
                await Task.Delay(150); // Wait for fade out to complete
                _pinToolTipPopup.IsOpen = false;
            }
        }

        private async void LanguageButton_PointerEntered(object? sender, PointerEventArgs e)
        {
            if (_langToolTipPopup != null)
            {
                _langToolTipPopup.PlacementTarget = _languageButton;
                _langToolTipPopup.IsOpen = true;
                await Task.Delay(10); // Ensure popup is positioned before fading in
                _langToolTipPopup.Opacity = 1;
            }
        }

        private async void LanguageButton_PointerExited(object? sender, PointerEventArgs e)
        {
            if (_langToolTipPopup != null)
            {
                _langToolTipPopup.Opacity = 0;
                await Task.Delay(150); // Wait for fade out to complete
                _langToolTipPopup.IsOpen = false;
            }
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

                if (_clickCount == 1)
                {
                    _doubleClickTimer.Start();
                }
                else if (_clickCount == 2)
                {
                    _doubleClickTimer.Stop();
                    _clickCount = 0;
                    
                    ToggleTopmost();
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

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            // 绛変竴甯ф覆鏌撳啀鎾紙鏇村鏄撯€滆倝鐪煎彲瑙佲€濓級
            _ = Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await AnimateFadeIn();
            }, DispatcherPriority.Render);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == WindowStateProperty)
            {
                var newState = (WindowState?)change.NewValue;
                
                // 澶勭悊浠庝换鍔℃爮鐐瑰嚮鎭㈠ (Normal/Maximized)
                if ((newState == WindowState.Normal || newState == WindowState.Maximized) && Opacity < 1.0)
                {
                    _ = Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        await Task.Delay(50); // 绛夊緟绯荤粺鍔ㄧ敾
                        await AnimateFadeIn();
                    }, DispatcherPriority.Render);
                }
                // 澶勭悊閫氳繃浠诲姟鏍忕偣鍑绘渶灏忓寲
                else if (newState == WindowState.Minimized && Opacity > 0)
                {
                    // 娉ㄦ剰锛氳繖閲屽叾瀹炵郴缁熷凡缁忓紑濮嬫渶灏忓寲浜嗭紝鎴戜滑鍐嶅仛鍔ㄧ敾鍙兘鏉ヤ笉鍙?
                    // 鎴栬€呬細鍜岀郴缁熸渶灏忓寲鍔ㄧ敾鍙犲姞銆備絾涓轰簡淇濇寔鐘舵€佷竴鑷达紝璁句负 0 鏄繀瑕佺殑銆?
                    // 涔熷彲浠ュ皾璇曟挱鏀句竴涓揩閫熺殑 fade out
                    Opacity = 0;
                }
            }
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            // 宸茬粡鍦ㄥ姩鐢诲叧闂祦绋嬮噷浜嗭紝灏辨斁琛?
            if (_closingAnimating)
            {
                base.OnClosing(e);
                return;
            }

            // 鍙湁鐢ㄦ埛瑙﹀彂鐨勫叧闂墠鎷︽埅锛堜綘鍘熼€昏緫 OK锛?
            if (!e.IsProgrammatic)
            {
                e.Cancel = true;
                _closingAnimating = true;

                // 涓€瀹氭斁鍒?UI 绾跨▼闃熷垪閲岃窇锛岄伩鍏嶆椂搴?绾跨▼闂
                Dispatcher.UIThread.Post(async () =>
                {
                    try
                    {
                        // 纭繚璧峰鍙
                        Opacity = 1;
                        await AnimateFadeOut();

                        // 鍔ㄧ敾缁撴潫鍚庡啀鐪熸鍏抽棴
                        Close();
                    }
                    finally
                    {
                        _closingAnimating = false;
                    }
                });

                return;
            }

            base.OnClosing(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.Escape && _editView?.IsVisible == true)
            {
                GoToHomeView();
                e.Handled = true;
                return;
            }

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
            // var hoverIconService = App.Services!.GetRequiredService<HoverIconService>();
            // if (!_viewModel.IsDragOver)
            // {
            //     hoverIconService.ShowPinIcon();
            //     hoverIconService.UpdatePosition(e.GetPosition(this));
            // }
        }

        private void MainWindow_PointerExited(object? sender, PointerEventArgs e)
        {
            // var hoverIconService = App.Services!.GetRequiredService<HoverIconService>();
            // hoverIconService.Hide();
        }

        private void DoubleClickTimer_Tick(object? sender, EventArgs e)
        {
            _doubleClickTimer.Stop();
            _clickCount = 0;
            // var hoverIconService = App.Services!.GetRequiredService<HoverIconService>();
            // hoverIconService.ShowPinIcon("Ready");
        }

        private void DragAndDropTarget_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data.Contains(DataFormats.Files))
            {
                _viewModel.IsDragOver = true;
                e.DragEffects = DragDropEffects.Link;
                // var hoverIconService = App.Services!.GetRequiredService<HoverIconService>();
                // hoverIconService.ShowFileIcon(e.Data, _viewModel.FolderPath);
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void DragAndDropTarget_DragOver(object? sender, DragEventArgs e)
        {
            // var hoverIconService = App.Services!.GetRequiredService<HoverIconService>();
            // hoverIconService.UpdatePosition(e.GetPosition(this));

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
            // var hoverIconService = App.Services!.GetRequiredService<HoverIconService>();
            // hoverIconService.Hide();
            e.Handled = true;
        }

        private void DragAndDropTarget_Drop(object? sender, DragEventArgs e)
        {
            _viewModel.IsDragOver = false;
            // var hoverIconService = App.Services!.GetRequiredService<HoverIconService>();
            // hoverIconService.Hide();
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

        public async Task AnimateFadeIn()
        {
            // Ensure RenderTransform exists for scaling
            if (this.RenderTransform is not ScaleTransform)
            {
                this.RenderTransform = new ScaleTransform();
            }
            this.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);

            var animation = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(150),
                Easing = new CubicEaseOut(),
                FillMode = FillMode.Forward, // 鍏抽敭锛氬仠鐣欏湪鏈€鍚庝竴甯?
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0),
                        Setters =
                        {
                            new Setter(OpacityProperty, 0.0),
                            new Setter(ScaleTransform.ScaleXProperty, 0.95),
                            new Setter(ScaleTransform.ScaleYProperty, 0.95)
                        }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1),
                        Setters =
                        {
                            new Setter(OpacityProperty, 1.0),
                            new Setter(ScaleTransform.ScaleXProperty, 1.0),
                            new Setter(ScaleTransform.ScaleYProperty, 1.0)
                        }
                    }
                }
            };

            await animation.RunAsync(this);
        }

        public async Task AnimateFadeOut()
        {
            // Ensure RenderTransform exists for scaling
            if (this.RenderTransform is not ScaleTransform)
            {
                this.RenderTransform = new ScaleTransform();
            }
            this.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);

            var animation = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(200),
                Easing = new CubicEaseOut(),
                FillMode = FillMode.Forward, // 鍏抽敭锛氬惁鍒欎細鍥炲脊
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0),
                        Setters =
                        {
                            new Setter(OpacityProperty, 1.0),
                            new Setter(ScaleTransform.ScaleXProperty, 1.0),
                            new Setter(ScaleTransform.ScaleYProperty, 1.0)
                        }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1),
                        Setters =
                        {
                            new Setter(OpacityProperty, 0.0),
                            new Setter(ScaleTransform.ScaleXProperty, 0.95),
                            new Setter(ScaleTransform.ScaleYProperty, 0.95)
                        }
                    }
                }
            };

            await animation.RunAsync(this);
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
            ToggleTopmost();
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
        
        private void UpdatePinButtonIcon()
        {
            if (_pinButtonIcon == null) return;
    
            _pinButtonIcon.Path = ConfigManager.Config.PinIcon.PinnedIcon;

            if (_pinButton != null)
            {
                var pinnedBackground = this.Topmost ? "#7CDDDDDD" : "#50FFFFFF";
                _pinButton.Background = new SolidColorBrush(Color.Parse(pinnedBackground));
                _pinButton.Classes.Set("pinned", this.Topmost);
            }

            if (_languageButton != null)
            {
                _languageButton.Background = new SolidColorBrush(Color.Parse("#50FFFFFF"));
            }

            _pinButtonIcon.Opacity = this.Topmost ? 0.82 : 0.4;
            UpdatePinGlowVisual();
        }

        private void ToggleTopmost()
        {
            this.Topmost = !this.Topmost;
            UpdatePinButtonIcon();

            var toastService = App.Services!.GetRequiredService<IToastService>();
            var message = this.Topmost
                ? LocalizationManager.Instance["Toast_WindowPinned"]
                : LocalizationManager.Instance["Toast_WindowUnpinned"];
            toastService.Show(message, new SolidColorBrush(Color.Parse("#EBB762")));
        }

        private void UpdatePinGlowVisual()
        {
            if (_pinIconGlow == null)
            {
                return;
            }

            if (this.Topmost)
            {
                if (!_pinGlowTimer.IsEnabled)
                {
                    _pinGlowPhase = 0;
                    _pinGlowTimer.Start();
                }

                ApplyPinGlowFrame();
                return;
            }

            _pinGlowTimer.Stop();
            _pinGlowPhase = 0;
            _pinIconGlow.Opacity = 0;
            if (_pinIconGlow.RenderTransform is ScaleTransform scale)
            {
                scale.ScaleX = 1;
                scale.ScaleY = 1;
            }
        }

        private void PinGlowTimer_Tick(object? sender, EventArgs e)
        {
            if (!this.Topmost || _pinIconGlow == null)
            {
                UpdatePinGlowVisual();
                return;
            }

            _pinGlowPhase += 0.08;
            ApplyPinGlowFrame();
        }

        private void ApplyPinGlowFrame()
        {
            if (_pinIconGlow == null)
            {
                return;
            }

            var pulse = (Math.Sin(_pinGlowPhase) + 1) * 0.5;
            _pinIconGlow.Opacity = 0.52 + pulse * 0.34;

            if (_pinIconGlow.RenderTransform is ScaleTransform scale)
            {
                var glowScale = 1.02 + pulse * 0.28;
                scale.ScaleX = glowScale;
                scale.ScaleY = glowScale;
            }
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

