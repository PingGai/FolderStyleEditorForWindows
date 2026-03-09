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
using Avalonia.Interactivity;
using System;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Threading;
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
        private readonly DragIntentAnalyzerService _dragIntentAnalyzerService;
        private readonly InterruptDialogService _interruptDialogService;
        private DragIntentResult _lastDragIntent = DragIntentResult.Unsupported;
        private CancellationTokenSource? _dragIntentCts;
        private CancellationTokenSource? _dragLeaveCts;
        private int _dragIntentVersion;
        private string? _lastOverlaySignature;
        private bool _isDropHandling;
        private readonly DispatcherTimer _dragOverlayWatchdogTimer;
        private DateTime _lastDragHeartbeatUtc;
        private Popup? _languagePopup;
        private DateTime _lastQualifiedTapReleaseUtc = DateTime.MinValue;
        private bool _pendingWindowTapCandidate;
        private bool _dragStartedForCurrentPress;
        private Point _pressPointInWindow;
        private DateTime _pressStartedUtc = DateTime.MinValue;
        private PointerPressedEventArgs? _pressEventForMoveDrag;
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
        private double _lastEditScrollOffsetY;
        
        private readonly FrameLimiter _limiter = new(60);
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private bool _isAnimating = true;
        private bool _closingAnimating;
        private const double DragStartThresholdPx = 7.0;
        private const double TapMaxMoveThresholdPx = 10.0;
        private const int TapMaxDurationMs = 220;
 
        [SupportedOSPlatform("windows")]
        public MainWindow()
        {
// #if DEBUG
//             RendererDiagnostics.DebugOverlays =
//                 RendererDebugOverlays.Fps | RendererDebugOverlays.DirtyRects;
// #endif
            InitializeComponent();
            
            // 启动时先设为 0，在 OnOpened 中再做渐入，避免窗口显示过程闪烁
            Opacity = 0;
 
            _viewModel = App.Services!.GetRequiredService<MainViewModel>();
            _sessionManager = new EditSessionManager(_viewModel);
            _dragIntentAnalyzerService = App.Services!.GetRequiredService<DragIntentAnalyzerService>();
            _interruptDialogService = App.Services!.GetRequiredService<InterruptDialogService>();
            _viewModel.NavigateToEditView = (folderPath, iconSourcePath) => GoToEditView(folderPath, iconSourcePath);
            this.DataContext = _viewModel;

            _pinGlowTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            _pinGlowTimer.Tick += PinGlowTimer_Tick;
            _dragOverlayWatchdogTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(120)
            };
            _dragOverlayWatchdogTimer.Tick += DragOverlayWatchdogTimer_Tick;
            
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
                var ui = ConfigManager.Config.Ui;
                dragDropIndicator.Stroke = new SolidColorBrush(Color.Parse(ui.DragIndicatorStrokeColor))
                {
                    Opacity = ui.DragIndicatorStrokeOpacity
                };
            }

            // MainWindow now handles all drag-drop logic globally.
            this.AddHandler(DragDrop.DragEnterEvent, DragAndDropTarget_DragEnter, RoutingStrategies.Bubble, handledEventsToo: true);
            this.AddHandler(DragDrop.DragOverEvent, DragAndDropTarget_DragOver, RoutingStrategies.Bubble, handledEventsToo: true);
            this.AddHandler(DragDrop.DragLeaveEvent, DragAndDropTarget_DragLeave, RoutingStrategies.Bubble, handledEventsToo: true);
            this.AddHandler(DragDrop.DropEvent, DragAndDropTarget_Drop, RoutingStrategies.Bubble, handledEventsToo: true);
            
            this.Loaded += (s, e) => StartFlowLoop();
            Activated += MainWindow_Activated;
            Deactivated += MainWindow_Deactivated;
            
            UpdatePinButtonIcon();
        }

        private void MainWindow_Deactivated(object? sender, EventArgs e)
        {
            if (_viewModel.IsDragOver || _interruptDialogService.State.IsPassiveOverlayVisible)
            {
                ResetDragOverlayState();
            }

            if (_editView != null && _editView.IsVisible)
            {
                _lastEditScrollOffsetY = _editView.GetEditScrollOffsetY();
            }
        }

        private void MainWindow_Activated(object? sender, EventArgs e)
        {
            if (_editView == null || !_editView.IsVisible)
            {
                return;
            }

            var offset = _lastEditScrollOffsetY;
            Dispatcher.UIThread.Post(() =>
            {
                _editView?.RestoreEditScrollOffsetY(offset);
            }, DispatcherPriority.Background);
            Dispatcher.UIThread.Post(() =>
            {
                _editView?.RestoreEditScrollOffsetY(offset);
            }, DispatcherPriority.Input);
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
            }

            // New gesture pipeline:
            // press -> move threshold starts drag, release decides click.
            if (!isInteractiveControl && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _pendingWindowTapCandidate = true;
                _dragStartedForCurrentPress = false;
                _pressPointInWindow = e.GetPosition(this);
                _pressStartedUtc = DateTime.UtcNow;
                _pressEventForMoveDrag = e;
                return;
            }

            _pendingWindowTapCandidate = false;
            _dragStartedForCurrentPress = false;
            _pressEventForMoveDrag = null;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            if (!_pendingWindowTapCandidate || _dragStartedForCurrentPress)
            {
                return;
            }

            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            var current = e.GetPosition(this);
            var dx = current.X - _pressPointInWindow.X;
            var dy = current.Y - _pressPointInWindow.Y;
            var movedDistanceSq = dx * dx + dy * dy;
            if (movedDistanceSq < DragStartThresholdPx * DragStartThresholdPx)
            {
                return;
            }

            _dragStartedForCurrentPress = true;
            _pendingWindowTapCandidate = false;
            _lastQualifiedTapReleaseUtc = DateTime.MinValue;

            var dragArgs = _pressEventForMoveDrag;
            _pressEventForMoveDrag = null;
            if (dragArgs != null)
            {
                BeginMoveDrag(dragArgs);
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);

            if (!_pendingWindowTapCandidate)
            {
                _pressEventForMoveDrag = null;
                return;
            }

            var source = e.Source as Control;
            var isInteractiveControl = source?.FindAncestorOfType<TextBox>() != null ||
                                       source?.FindAncestorOfType<Button>() != null ||
                                       source?.FindAncestorOfType<Popup>() != null;
            if (isInteractiveControl)
            {
                _pendingWindowTapCandidate = false;
                _dragStartedForCurrentPress = false;
                _pressEventForMoveDrag = null;
                return;
            }

            var releasePoint = e.GetPosition(this);
            var dx = releasePoint.X - _pressPointInWindow.X;
            var dy = releasePoint.Y - _pressPointInWindow.Y;
            var movedDistanceSq = dx * dx + dy * dy;
            var pressDuration = DateTime.UtcNow - _pressStartedUtc;

            _pendingWindowTapCandidate = false;
            _dragStartedForCurrentPress = false;
            _pressEventForMoveDrag = null;

            var isQuickTap =
                pressDuration <= TimeSpan.FromMilliseconds(TapMaxDurationMs) &&
                movedDistanceSq < TapMaxMoveThresholdPx * TapMaxMoveThresholdPx;
            if (!isQuickTap)
            {
                _lastQualifiedTapReleaseUtc = DateTime.MinValue;
                return;
            }

            var now = DateTime.UtcNow;
            var threshold = TimeSpan.FromMilliseconds(ConfigManager.Config.Features.Features.PinDoubleClickThreshold);
            if (_lastQualifiedTapReleaseUtc != DateTime.MinValue &&
                now - _lastQualifiedTapReleaseUtc <= threshold)
            {
                _lastQualifiedTapReleaseUtc = DateTime.MinValue;
                ToggleTopmost();
                return;
            }

            _lastQualifiedTapReleaseUtc = now;
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
                    // 最小化时不再播放淡出动画，否则会从当前状态直接切到最小化
                    // 恢复时若没有合适的初始值，窗口会从 0 透明度突兀出现
                    // 这里直接将透明度归零，避免淡出动画干扰窗口状态切换
                    Opacity = 0;
                }
            }
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            // 已经处于关闭流程中时，避免重复触发关闭逻辑
            if (_closingAnimating)
            {
                base.OnClosing(e);
                return;
            }

            // 非程序内主动关闭时，先拦截关闭并播放渐出动画，再真正关闭窗口
            if (!e.IsProgrammatic)
            {
                e.Cancel = true;
                _closingAnimating = true;

                // 把动画安排到 UI 队列里执行，避免在关闭事件中直接阻塞
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

            if (_interruptDialogService.State.IsActive)
            {
                return;
            }

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

        private void MainWindow_PointerExited(object? sender, PointerEventArgs e)
        {
            // var hoverIconService = App.Services!.GetRequiredService<HoverIconService>();
            // hoverIconService.Hide();
        }

        private void DragAndDropTarget_DragEnter(object? sender, DragEventArgs e)
        {
            try
            {
                _dragLeaveCts?.Cancel();
                TouchDragHeartbeat();
                _viewModel.IsDragOver = true;
                var intent = _dragIntentAnalyzerService.AnalyzeImmediate(e.Data, ResolveDragContext(), _viewModel.FolderPath);
                _lastDragIntent = intent;
                e.DragEffects = DragDropEffects.Link;
                ShowDragOverlay(intent);
                UpdatePassiveOverlayMotion(e);
                _ = RefreshDragIntentAsync(e);
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DragEnter failed: {ex}");
                ResetDragOverlayState();
                e.DragEffects = DragDropEffects.None;
                e.Handled = true;
            }
        }

        private void DragAndDropTarget_DragOver(object? sender, DragEventArgs e)
        {
            try
            {
                _dragLeaveCts?.Cancel();
                TouchDragHeartbeat();
                _viewModel.IsDragOver = true;
                var intent = _dragIntentAnalyzerService.AnalyzeImmediate(e.Data, ResolveDragContext(), _viewModel.FolderPath);
                _lastDragIntent = intent;
                e.DragEffects = DragDropEffects.Link;
                ShowDragOverlay(intent);
                UpdatePassiveOverlayMotion(e);
                _ = RefreshDragIntentAsync(e);
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DragOver failed: {ex}");
                ResetDragOverlayState();
                e.DragEffects = DragDropEffects.None;
                e.Handled = true;
            }
        }

        private async void DragAndDropTarget_DragLeave(object? sender, DragEventArgs e)
        {
            if (_isDropHandling)
            {
                e.Handled = true;
                return;
            }

            _dragLeaveCts?.Cancel();
            var cts = new CancellationTokenSource();
            _dragLeaveCts = cts;

            try
            {
                await Task.Delay(90, cts.Token);
            }
            catch (OperationCanceledException)
            {
                e.Handled = true;
                return;
            }

            if (cts.IsCancellationRequested)
            {
                e.Handled = true;
                return;
            }

            if (IsDragStillInsideWindow(e))
            {
                e.Handled = true;
                return;
            }

            ResetDragOverlayState();
            e.Handled = true;
        }

        private async void DragAndDropTarget_Drop(object? sender, DragEventArgs e)
        {
            if (_isDropHandling)
            {
                e.Handled = true;
                return;
            }

            try
            {
                _isDropHandling = true;
                _dragLeaveCts?.Cancel();
                _dragIntentCts?.Cancel();

                var dropIntent = await _dragIntentAnalyzerService.AnalyzeAsync(
                    e.Data,
                    ResolveDragContext(),
                    _viewModel.FolderPath,
                    CancellationToken.None);
                _viewModel.IsDragOver = false;
                ResetDragOverlayState();
                e.Handled = true;

                var context = ResolveDragContext();
                var files = e.Data.GetFiles()?.ToList() ?? new System.Collections.Generic.List<IStorageItem>();
                var firstPath = files.FirstOrDefault()?.Path.LocalPath ?? string.Empty;

                switch (dropIntent.Type)
                {
                    case DragIntentType.SingleFolder:
                        if (!string.IsNullOrWhiteSpace(firstPath) && Directory.Exists(firstPath))
                        {
                            await _viewModel.StartEditSessionAsync(firstPath);
                        }
                        break;
                    case DragIntentType.Ico:
                    case DragIntentType.ExeInternal:
                    case DragIntentType.ExeExternal:
                        if (context == DragContext.Edit && !string.IsNullOrWhiteSpace(firstPath) && File.Exists(firstPath))
                        {
                            _viewModel.IconPath = firstPath;
                        }
                        break;
                    case DragIntentType.Text:
                        if (context == DragContext.Edit && e.Data.Contains(DataFormats.Text))
                        {
                            var text = e.Data.GetText()?.Trim().Trim('"', '\'');
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                _viewModel.Alias = text;
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Drop failed: {ex}");
                ResetDragOverlayState();
                e.DragEffects = DragDropEffects.None;
                e.Handled = true;
            }
            finally
            {
                _isDropHandling = false;
            }
        }

        private DragContext ResolveDragContext()
        {
            return _editView?.IsVisible == true ? DragContext.Edit : DragContext.Home;
        }

        private async Task RefreshDragIntentAsync(DragEventArgs e)
        {
            var currentVersion = Interlocked.Increment(ref _dragIntentVersion);
            _dragIntentCts?.Cancel();
            _dragIntentCts = new CancellationTokenSource();
            var token = _dragIntentCts.Token;

            DragIntentResult result;
            try
            {
                result = await _dragIntentAnalyzerService.AnalyzeAsync(
                    e.Data,
                    ResolveDragContext(),
                    _viewModel.FolderPath,
                    token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RefreshDragIntentAsync failed: {ex.Message}");
                result = DragIntentResult.Unsupported;
            }

            if (currentVersion != _dragIntentVersion)
            {
                return;
            }

            _lastDragIntent = result;
            e.DragEffects = DragDropEffects.Link;
            ShowDragOverlay(result);
        }

        private void ShowDragOverlay(DragIntentResult intent)
        {
            var loc = LocalizationManager.Instance;
            var mainText = loc[intent.MainTextKey];
            var subText = string.IsNullOrWhiteSpace(intent.SubTextKey) ? null : loc[intent.SubTextKey];
            var signature = $"{intent.Type}|{mainText}|{subText}|{intent.IconPath}|{intent.SubTextBrush}";
            if (string.Equals(_lastOverlaySignature, signature, StringComparison.Ordinal))
            {
                return;
            }

            var ui = ConfigManager.Config.Ui;
            var isWarningMain = intent.Type == DragIntentType.Unsupported;
            var mainBrush = new SolidColorBrush(Color.Parse(isWarningMain ? ui.DragOverlayWarningTextColor : ui.DragOverlayMainTextColor));
            var subBrush = new SolidColorBrush(Color.Parse(string.IsNullOrWhiteSpace(intent.SubTextBrush) ? ui.DragOverlayWarningTextColor : intent.SubTextBrush));

            _interruptDialogService.ShowPassiveOverlay(new InterruptDialogOptions
            {
                Title = mainText,
                TitleForeground = mainBrush,
                CenterIconPath = intent.IconPath,
                SubText = subText,
                SubTextForeground = subBrush,
                ShowPrimaryButton = ConfigManager.Config.DragOverlay.ShowPrimaryButton,
                ShowSecondaryButton = ConfigManager.Config.DragOverlay.ShowSecondaryButton,
                DismissOnEsc = ConfigManager.Config.DragOverlay.DismissOnEsc,
                AllowOverlayClickDismiss = ConfigManager.Config.DragOverlay.AllowOverlayClickDismiss,
                HitTestVisible = false
            });

            _lastOverlaySignature = signature;
        }

        private void ResetDragOverlayState()
        {
            _viewModel.IsDragOver = false;
            _lastDragIntent = DragIntentResult.Unsupported;
            _lastOverlaySignature = null;
            Interlocked.Increment(ref _dragIntentVersion);
            _dragIntentCts?.Cancel();
            _dragOverlayWatchdogTimer.Stop();
            _interruptDialogService.HidePassiveOverlay();
        }

        private void UpdatePassiveOverlayMotion(DragEventArgs e)
        {
            if (!_viewModel.IsDragOver)
            {
                return;
            }

            var position = e.GetPosition(this);
            _interruptDialogService.UpdatePassiveOverlayMotion(position.X, position.Y, Bounds.Width, Bounds.Height);
        }

        private void TouchDragHeartbeat()
        {
            _lastDragHeartbeatUtc = DateTime.UtcNow;
            if (!_dragOverlayWatchdogTimer.IsEnabled)
            {
                _dragOverlayWatchdogTimer.Start();
            }
        }

        private void DragOverlayWatchdogTimer_Tick(object? sender, EventArgs e)
        {
            if (!_viewModel.IsDragOver && !_interruptDialogService.State.IsPassiveOverlayVisible)
            {
                _dragOverlayWatchdogTimer.Stop();
                return;
            }

            if (DateTime.UtcNow - _lastDragHeartbeatUtc < TimeSpan.FromMilliseconds(220))
            {
                return;
            }

            ResetDragOverlayState();
        }

        private bool IsDragStillInsideWindow(DragEventArgs e)
        {
            var position = e.GetPosition(this);
            return position.X >= 0 &&
                   position.Y >= 0 &&
                   position.X <= Bounds.Width &&
                   position.Y <= Bounds.Height;
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
                FillMode = FillMode.Forward, // 播放完成后保持最后一帧
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

            if (_languagePopup.IsOpen)
            {
                if (_languagePopup.Child is { } closingContent)
                {
                    closingContent.RenderTransform = new Avalonia.Media.TranslateTransform();
                    var closeAnimation = new Animation
                    {
                        Duration = TimeSpan.FromMilliseconds(180),
                        Easing = new CubicEaseOut(),
                        FillMode = FillMode.Forward,
                        Children =
                        {
                            new KeyFrame
                            {
                                Cue = new Cue(0),
                                Setters =
                                {
                                    new Setter(OpacityProperty, 1.0),
                                    new Setter(Avalonia.Media.TranslateTransform.YProperty, 0.0)
                                }
                            },
                            new KeyFrame
                            {
                                Cue = new Cue(1),
                                Setters =
                                {
                                    new Setter(OpacityProperty, 0.0),
                                    new Setter(Avalonia.Media.TranslateTransform.YProperty, -8.0)
                                }
                            }
                        }
                    };
                    await closeAnimation.RunAsync(closingContent, System.Threading.CancellationToken.None);
                }

                _languagePopup.IsOpen = false;
                return;
            }

            _languagePopup.IsOpen = true;

            if (_languagePopup.Child is { } popupContent)
            {
                popupContent.RenderTransform = new Avalonia.Media.TranslateTransform();
                popupContent.Opacity = 0;
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
