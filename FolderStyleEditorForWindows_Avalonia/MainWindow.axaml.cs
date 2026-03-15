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
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Threading;
using Avalonia.Media;
using Avalonia.Threading;
using System.Collections.Specialized;
using FolderStyleEditorForWindows.ViewModels;
using FolderStyleEditorForWindows.Views;
using FolderStyleEditorForWindows.Services;
using Avalonia.Media.Immutable;

namespace FolderStyleEditorForWindows
{
    public partial class MainWindow : Window
    {
        private HomeView? _homeView;
        private EditView? _editView;
        private Grid? _rootLayer;
        private Border? _baseLayer;
        private Border? _flowLayer;
        private Grid? _cardsLayer;
        private MainViewModel _viewModel;
        private EditSessionManager _sessionManager;
        private readonly DragIntentAnalyzerService _dragIntentAnalyzerService;
        private readonly InterruptDialogService _interruptDialogService;
        private readonly ImageToIcoService _imageToIcoService;
        private readonly IToastService _toastService;
        private readonly DisplayInfoService _displayInfoService;
        private readonly AnimationStateSource _animationStateSource;
        private readonly FrameRateGovernor _frameRateGovernor;
        private readonly AmbientAnimationScheduler _ambientAnimationScheduler;
        private readonly RenderScheduler _renderScheduler;
        private readonly LayerInvalidationController _layerInvalidationController;
        private readonly FrameRateSettings _frameRateSettings;
        private readonly PerformanceTelemetryService _performanceTelemetryService;
        private readonly ComponentFpsBadgeSource _componentFpsBadgeSource;
        private readonly PerformanceMonitorViewModel _performanceMonitorViewModel;
        private DragIntentResult _lastDragIntent = DragIntentResult.Unsupported;
        private CancellationTokenSource? _dragIntentCts;
        private CancellationTokenSource? _dragLeaveCts;
        private int _dragIntentVersion;
        private string? _lastOverlaySignature;
        private string _imageDragSelectionKey = nameof(ImageFitMode.FillHeight);
        private bool _isDropHandling;
        private readonly DispatcherTimer _dragOverlayWatchdogTimer;
        private DateTime _lastDragHeartbeatUtc;
        private Popup? _languagePopup;
        private CancellationTokenSource? _languagePopupAnimationCts;
        private bool _languagePopupDesiredOpen;
        private DateTime _lastQualifiedTapReleaseUtc = DateTime.MinValue;
        private bool _pendingWindowTapCandidate;
        private bool _dragStartedForCurrentPress;
        private Point _pressPointInWindow;
        private DateTime _pressStartedUtc = DateTime.MinValue;
        private PointerPressedEventArgs? _pressEventForMoveDrag;
        private Avalonia.Svg.Skia.Svg? _pinButtonIcon;
        private Button? _pinButton;
        private Button? _languageButton;
        private StackPanel? _actionButtonsPanel;
        private Popup? _pinToolTipPopup;
        private Popup? _langToolTipPopup;
        private TextBlock? _pinToolTipTextBlock;
        private TextBlock? _langToolTipTextBlock;
        private Border? _pinIconGlow;
        private ScaleTransform? _pinGlowScaleTransform;
        private PerformanceMonitor? _performanceMonitor;
        private double _pinGlowPhase;
        private double _lastEditScrollOffsetY;
        private IAmbientAnimationHandle? _backgroundAmbientHandle;
        private IAmbientAnimationHandle? _pinGlowAmbientHandle;
        private LinearGradientBrush? _backgroundFlowBrush;
        private GradientStop? _backgroundFlowStartStop;
        private GradientStop? _backgroundFlowEndStop;
        private double _backgroundFlowPhase;
        private bool _isLowCostTransparencyActive;
        private bool _isWindowRuntimeSuspended;
        private bool _debugPinnedVisualState;
        private bool _isRenderLoopActive;
        private bool _isRenderFramePending;
        private readonly Dictionary<Control, DebugExcludedComponentState> _debugExcludedComponents = new();
        private readonly Dictionary<Control, DebugExcludedComponentState> _debugExcludedPlaceholders = new();
        private readonly Dictionary<string, SolidColorBrush> _dragOverlayBrushCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly DispatcherTimer _renderWakeTimer;
        private readonly DispatcherTimer _idleMemoryTrimTimer;
        private CancellationTokenSource? _windowRootAnimationCts;
        private bool _closingAnimating;
        private const double DragStartThresholdPx = 7.0;
        private const double TapMaxMoveThresholdPx = 10.0;
        private const int TapMaxDurationMs = 220;
        private const int ViewTransitionDurationMs = 380;
        private const int WindowTransitionDurationMs = 240;
        private const int PopupTransitionDurationMs = 320;
        private static readonly IBrush ActiveWindowBackground = Brushes.Transparent;
        private static readonly IBrush LowCostWindowBackground = new ImmutableSolidColorBrush(Colors.White);
        private static readonly SolidColorBrush AccentToastBrush = new(Color.Parse("#EBB762"));
        private static readonly SolidColorBrush PinButtonPinnedBackgroundBrush = new(Color.Parse("#7CDDDDDD"));
        private static readonly SolidColorBrush PinButtonDefaultBackgroundBrush = new(Color.Parse("#50FFFFFF"));
        private static readonly SolidColorBrush DebugExcludedPlaceholderBackgroundBrush = new(Color.Parse("#14D56A61"));
        private static readonly SolidColorBrush DebugExcludedPlaceholderBorderBrush = new(Color.Parse("#E07167"));
        private static readonly SolidColorBrush DebugExcludedPlaceholderForegroundBrush = new(Color.Parse("#C45E56"));

        private sealed class DebugExcludedComponentState
        {
            public required Control OriginalControl { get; init; }
            public required Border PlaceholderControl { get; init; }
            public required Panel ParentPanel { get; init; }
            public required int ChildIndex { get; init; }
        }
 
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
            _imageToIcoService = App.Services!.GetRequiredService<ImageToIcoService>();
            _toastService = App.Services!.GetRequiredService<IToastService>();
            _displayInfoService = App.Services!.GetRequiredService<DisplayInfoService>();
            _animationStateSource = App.Services!.GetRequiredService<AnimationStateSource>();
            _frameRateGovernor = App.Services!.GetRequiredService<FrameRateGovernor>();
            _ambientAnimationScheduler = App.Services!.GetRequiredService<AmbientAnimationScheduler>();
            _renderScheduler = App.Services!.GetRequiredService<RenderScheduler>();
            _layerInvalidationController = App.Services!.GetRequiredService<LayerInvalidationController>();
            _frameRateSettings = App.Services!.GetRequiredService<FrameRateSettings>();
            _performanceTelemetryService = App.Services!.GetRequiredService<PerformanceTelemetryService>();
            _componentFpsBadgeSource = App.Services!.GetRequiredService<ComponentFpsBadgeSource>();
            _performanceMonitorViewModel = App.Services!.GetRequiredService<PerformanceMonitorViewModel>();
            _frameRateSettings.PropertyChanged += FrameRateSettings_PropertyChanged;
            _animationStateSource.StateChanged += AnimationStateSource_StateChanged;
            DebugRuntimeAnalysis.PauseAnimationsChanged += DebugRuntimeAnalysis_PauseAnimationsChanged;
            _viewModel.NavigateToEditView = (folderPath, iconSourcePath) => GoToEditView(folderPath, iconSourcePath);
            this.DataContext = _viewModel;

            _dragOverlayWatchdogTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(120)
            };
            _dragOverlayWatchdogTimer.Tick += DragOverlayWatchdogTimer_Tick;
            _renderWakeTimer = new DispatcherTimer();
            _renderWakeTimer.Tick += RenderWakeTimer_Tick;
            _idleMemoryTrimTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(18)
            };
            _idleMemoryTrimTimer.Tick += IdleMemoryTrimTimer_Tick;
            
            _homeView = this.FindControl<HomeView>("HomeView");
            _editView = this.FindControl<EditView>("EditView");
            _languagePopup = this.FindControl<Popup>("LanguagePopup");
            _pinButtonIcon = this.FindControl<Avalonia.Svg.Skia.Svg>("PinButtonIcon");
            _pinButton = this.FindControl<Button>("PinButton");
            _languageButton = this.FindControl<Button>("LanguageButton");
            _actionButtonsPanel = this.FindControl<StackPanel>("ActionButtonsPanel");
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
            _rootLayer = this.FindControl<Grid>("RootLayer");
            _flowLayer = this.FindControl<Border>("FlowLayer");
            _cardsLayer = this.FindControl<Grid>("CardsLayer");
            EnsureBackgroundFlowBrush();
            _layerInvalidationController.Bind(RenderLayer.Background, () => _baseLayer?.InvalidateVisual());
            _layerInvalidationController.Bind(RenderLayer.Ambient, () => _flowLayer?.InvalidateVisual());
            _layerInvalidationController.Bind(RenderLayer.Content, () => _cardsLayer?.InvalidateVisual());
            _layerInvalidationController.Bind(RenderLayer.Overlay, () => this.InvalidateVisual());
            _layerInvalidationController.Bind(RenderLayer.Static, () => _baseLayer?.InvalidateVisual());

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

            var performanceMonitorControl = this.FindControl<Control>("PerformanceMonitorControl");
            var performanceMonitorPopup = this.FindControl<Popup>("PerformanceMonitorPopup");
            if (performanceMonitorPopup != null)
            {
                performanceMonitorPopup.DataContext = _performanceMonitorViewModel;
            }

            if (performanceMonitorControl != null)
            {
                performanceMonitorControl.DataContext = _performanceMonitorViewModel;
                if (performanceMonitorControl is PerformanceMonitor performanceMonitor)
                {
                    _performanceMonitor = performanceMonitor;
                    performanceMonitor.SetDragHost(this);
                }
            }

            var performanceBadgeLayer = this.FindControl<Control>("PerformanceBadgeLayer");
            if (performanceBadgeLayer != null)
            {
                performanceBadgeLayer.DataContext = _componentFpsBadgeSource;
            }

            // MainWindow now handles all drag-drop logic globally.
            this.AddHandler(DragDrop.DragEnterEvent, DragAndDropTarget_DragEnter, RoutingStrategies.Bubble, handledEventsToo: true);
            this.AddHandler(DragDrop.DragOverEvent, DragAndDropTarget_DragOver, RoutingStrategies.Bubble, handledEventsToo: true);
            this.AddHandler(DragDrop.DragLeaveEvent, DragAndDropTarget_DragLeave, RoutingStrategies.Bubble, handledEventsToo: true);
            this.AddHandler(DragDrop.DropEvent, DragAndDropTarget_Drop, RoutingStrategies.Bubble, handledEventsToo: true);
            this.AddHandler(InputElement.PointerWheelChangedEvent, MainWindow_PointerWheelChanged, RoutingStrategies.Tunnel, handledEventsToo: true);
            this.AddHandler(InputElement.PointerPressedEvent, MainWindow_TunnelPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);

            if (_viewModel.Toasts is INotifyCollectionChanged notifyCollection)
            {
                notifyCollection.CollectionChanged += Toasts_CollectionChanged;
                _animationStateSource.SetToastAnimating(_viewModel.Toasts.Count > 0);
                _componentFpsBadgeSource.SetToastVisible(_viewModel.Toasts.Count > 0);
            }

            _backgroundAmbientHandle = _ambientAnimationScheduler.Register(
                "main-background-flow",
                () => _frameRateSettings.BackgroundAmbientFps,
                TickBackgroundAmbientFlow);
            _pinGlowAmbientHandle = _ambientAnimationScheduler.Register(
                "window-pin-glow",
                () => _frameRateSettings.HomeTitleAmbientFps,
                TickPinGlowAmbient);
            _pinGlowAmbientHandle.SetEnabled(false);

            this.Loaded += (_, _) =>
            {
                _displayInfoService.Refresh();
                StartRenderLoop();
                _idleMemoryTrimTimer.Start();
                _animationStateSource.MarkStaticDirty();
            };
            Closed += (_, _) => _sessionManager.Dispose();
            Activated += MainWindow_Activated;
            Deactivated += MainWindow_Deactivated;
            PropertyChanged += MainWindow_PropertyChanged;
            
            ApplyDebugExclusions();
            UpdatePinButtonIcon();
        }

        private bool IsPinnedVisualActive => _frameRateSettings.ExcludeActualTopmost ? _debugPinnedVisualState : this.Topmost;

        private void MainWindow_Deactivated(object? sender, EventArgs e)
        {
            _performanceMonitorViewModel.SetHostActive(false);
            DismissTransientPopupUi();
            _animationStateSource.SetDragging(false);
            _animationStateSource.SetTransitionAnimating(false);
            if (_viewModel.IsDragOver || _interruptDialogService.State.IsPassiveOverlayVisible)
            {
                ResetDragOverlayState();
            }

            if (_editView != null && _editView.IsVisible)
            {
                _lastEditScrollOffsetY = _editView.GetEditScrollOffsetY();
            }
            _animationStateSource.MarkStaticDirty();
        }

        private void MainWindow_Activated(object? sender, EventArgs e)
        {
            _performanceMonitorViewModel.SetHostActive(true);
            _displayInfoService.Refresh();
            if (_languagePopupDesiredOpen && _languagePopup is { IsOpen: true })
            {
                _ = SetLanguagePopupOpenAsync(false);
            }
            _animationStateSource.MarkStaticDirty();
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

        private void MainWindow_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == BoundsProperty)
            {
                _animationStateSource.MarkStaticDirty();
            }
        }

        private void FrameRateSettings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(FrameRateSettings.ExcludePinGlow):
                case nameof(FrameRateSettings.EnableComponentExcludeMode):
                case nameof(FrameRateSettings.ExcludeBottomActionButtons):
                case nameof(FrameRateSettings.ExcludeActualTopmost):
                    ApplyDebugExclusions();
                    break;
            }
        }

        private void ApplyDebugExclusions()
        {
            if (_actionButtonsPanel != null)
            {
                _actionButtonsPanel.IsVisible = !_frameRateSettings.ExcludeBottomActionButtons;
            }

            if (_frameRateSettings.ExcludeActualTopmost)
            {
                _debugPinnedVisualState = _debugPinnedVisualState || this.Topmost;
                if (this.Topmost)
                {
                    this.Topmost = false;
                }
            }
            else
            {
                _debugPinnedVisualState = false;
            }

            if (!_frameRateSettings.EnableComponentExcludeMode)
            {
                RestoreAllDebugExcludedComponents();
            }

            UpdatePinButtonIcon();
            _animationStateSource.MarkStaticDirty();
        }

        private void MainWindow_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            _animationStateSource.MarkScrollActivity();
        }

        private void Toasts_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            _animationStateSource.SetToastAnimating(_viewModel.Toasts.Count > 0);
            _componentFpsBadgeSource.SetToastVisible(_viewModel.Toasts.Count > 0);
            _animationStateSource.MarkStaticDirty();
        }

        private void PinButton_TemplateApplied(object? sender, TemplateAppliedEventArgs e)
        {
            _pinIconGlow = e.NameScope.Find<Border>("PinIconGlow");
            _pinGlowScaleTransform = _pinIconGlow?.RenderTransform as ScaleTransform;
            UpdatePinGlowVisual();
        }

        private async void PinButton_PointerEntered(object? sender, PointerEventArgs e)
        {
            _animationStateSource.MarkHoverActivity();
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
            _animationStateSource.MarkHoverActivity();
            if (_pinToolTipPopup != null)
            {
                _pinToolTipPopup.Opacity = 0;
                await Task.Delay(150); // Wait for fade out to complete
                _pinToolTipPopup.IsOpen = false;
            }
        }

        private async void LanguageButton_PointerEntered(object? sender, PointerEventArgs e)
        {
            _animationStateSource.MarkHoverActivity();
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
            _animationStateSource.MarkHoverActivity();
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
            if (e.Handled)
            {
                return;
            }

            var source = e.Source as Control;
            if (_languagePopup is { IsOpen: true } &&
                source?.FindAncestorOfType<Button>() != _languageButton)
            {
                _ = SetLanguagePopupOpenAsync(false);
            }

            var clickedInsidePerformanceMonitor = source?.FindAncestorOfType<PerformanceMonitor>() != null;
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && !clickedInsidePerformanceMonitor)
            {
                _performanceMonitor?.DismissContextMenu();
            }

            // Check if the click is on an interactive control that should retain focus.
            var isInteractiveControl = IsWindowTapInteractiveSource(source);

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
            _animationStateSource.MarkHoverActivity();

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
            var isInteractiveControl = IsWindowTapInteractiveSource(source);
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

            if (change.Property != WindowStateProperty)
            {
                return;
            }

            var newState = (WindowState?)change.NewValue;
            SetWindowRuntimeSuspended(newState == WindowState.Minimized);
            if (newState != WindowState.Minimized)
            {
                RestoreWindowPresentationState();
                _ = AnimateRestoreTransitionAsync();
            }
            _animationStateSource.MarkStaticDirty();
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            _isRenderLoopActive = false;
            _idleMemoryTrimTimer.Stop();
            DebugRuntimeAnalysis.PauseAnimationsChanged -= DebugRuntimeAnalysis_PauseAnimationsChanged;
            _backgroundAmbientHandle?.SetEnabled(false);
            _pinGlowAmbientHandle?.SetEnabled(false);
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
                _animationStateSource.SetDragging(true);
                _dragLeaveCts?.Cancel();
                TouchDragHeartbeat();
                _viewModel.IsDragOver = true;
                var intent = _dragIntentAnalyzerService.AnalyzeImmediate(e.Data, ResolveDragContext(), _viewModel.FolderPath);
                _lastDragIntent = intent;
                e.DragEffects = DragDropEffects.Link;
                ShowDragOverlay(intent);
                UpdatePassiveOverlayMotion(e);
                if (ShouldRefreshDragIntent(intent))
                {
                    _ = RefreshDragIntentAsync(e);
                }
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
                _animationStateSource.SetDragging(true);
                _dragLeaveCts?.Cancel();
                TouchDragHeartbeat();
                _viewModel.IsDragOver = true;
                var intent = _dragIntentAnalyzerService.AnalyzeImmediate(e.Data, ResolveDragContext(), _viewModel.FolderPath);
                _lastDragIntent = intent;
                e.DragEffects = DragDropEffects.Link;
                ShowDragOverlay(intent);
                UpdatePassiveOverlayMotion(e);
                if (ShouldRefreshDragIntent(intent))
                {
                    _ = RefreshDragIntentAsync(e);
                }
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

            _animationStateSource.SetDragging(false);
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
                _animationStateSource.SetDragging(false);
                _dragLeaveCts?.Cancel();
                _dragIntentCts?.Cancel();

                var dropIntent = await _dragIntentAnalyzerService.AnalyzeAsync(
                    e.Data,
                    ResolveDragContext(),
                    _viewModel.FolderPath,
                    CancellationToken.None);
                var imageDropSelectionKey = _imageDragSelectionKey;
                if (dropIntent.Type == DragIntentType.ImageToIcon)
                {
                    var dropPosition = e.GetPosition(this);
                    imageDropSelectionKey = _interruptDialogService.ResolvePassiveChoiceAt(
                        dropPosition.X,
                        dropPosition.Y,
                        _imageDragSelectionKey);
                }

                _viewModel.IsDragOver = false;
                ResetDragOverlayState();
                e.Handled = true;

                var context = ResolveDragContext();
                var firstPath = TryGetFirstStoragePath(e.Data) ?? string.Empty;

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
                    case DragIntentType.ImageToIcon:
                        if (context == DragContext.Edit && !string.IsNullOrWhiteSpace(firstPath) && File.Exists(firstPath))
                        {
                            await HandleDroppedImageToIconAsync(firstPath, imageDropSelectionKey);
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
                _animationStateSource.SetDragging(false);
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

        private static string? TryGetFirstStoragePath(IDataObject data)
        {
            if (data.GetFiles() is not { } files)
            {
                return null;
            }

            foreach (var item in files)
            {
                return item.Path.LocalPath;
            }

            return null;
        }

        private async Task HandleDroppedImageToIconAsync(string imagePath, string? preferredModeKey = null)
        {
            if (string.IsNullOrWhiteSpace(_viewModel.FolderPath) || !Directory.Exists(_viewModel.FolderPath))
            {
                return;
            }

            using var source = await _imageToIcoService.LoadPreviewAsync(imagePath, CancellationToken.None);
            var selectedMode = TryParseImageFitMode(preferredModeKey) ?? ImageFitMode.FillHeight;
            ImageCropSelection? manualSelection = null;
            if (selectedMode == ImageFitMode.ManualCrop)
            {
                var cropResult = await ShowManualCropDialogAsync(source);
                if (cropResult is null)
                {
                    return;
                }

                manualSelection = cropResult.Value;
            }

            await GenerateAndApplyDroppedImageIconAsync(imagePath, selectedMode, manualSelection);
        }

        private static ImageFitMode? TryParseImageFitMode(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            return Enum.TryParse<ImageFitMode>(key, out var parsed) ? parsed : null;
        }

        private async Task<ImageCropSelection?> ShowManualCropDialogAsync(LoadedImageToIcoSource source)
        {
            var loc = LocalizationManager.Instance;
            var cropField = new DialogImageCropFieldItem(
                loc["ImageToIco_Crop_Title"],
                source.PreviewBitmap,
                null,
                field => InitializeDefaultCropSelection(field, source.PixelWidth, source.PixelHeight));

            InitializeDefaultCropSelection(cropField, source.PixelWidth, source.PixelHeight);

            var response = await _interruptDialogService.ShowAsync(new InterruptDialogOptions
            {
                Title = loc["ImageToIco_Crop_Dialog_Title"],
                HeaderMeta = source.FileName,
                Content = loc["ImageToIco_Crop_Dialog_Content"],
                ContentTextAlignment = TextAlignment.Center,
                ContentHorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                PrimaryButtonText = loc["ImageToIco_Crop_Confirm"],
                SecondaryButtonText = loc["Dialog_Secondary_Cancel"],
                WidthRatio = 1.2,
                FormSections =
                [
                    new DialogFormSectionItem(
                        loc["ImageToIco_Crop_Section"],
                        new DialogFormFieldItem[] { cropField })
                ]
            });

            if (response.Result != InterruptDialogResult.Primary)
            {
                return null;
            }

            return new ImageCropSelection(
                cropField.SelectionX,
                cropField.SelectionY,
                cropField.SelectionWidth,
                cropField.SelectionHeight,
                cropField.CornerRadiusNormalized);
        }

        private static void InitializeDefaultCropSelection(DialogImageCropFieldItem cropField, int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                cropField.SetSelection(0, 0, 1, 1);
                cropField.SetCornerRadius(0);
                return;
            }

            if (width > height)
            {
                var normalizedWidth = height / (double)width;
                cropField.SetSelection((1 - normalizedWidth) / 2d, 0, normalizedWidth, 1);
                cropField.SetCornerRadius(0);
                return;
            }

            if (height > width)
            {
                var normalizedHeight = width / (double)height;
                cropField.SetSelection(0, (1 - normalizedHeight) / 2d, 1, normalizedHeight);
                cropField.SetCornerRadius(0);
                return;
            }

            cropField.SetSelection(0, 0, 1, 1);
            cropField.SetCornerRadius(0);
        }

        private async Task GenerateAndApplyDroppedImageIconAsync(string imagePath, ImageFitMode fitMode, ImageCropSelection? cropSelection)
        {
            var loc = LocalizationManager.Instance;

            try
            {
                var result = await _imageToIcoService.GenerateAsync(new ImageToIcoRequest
                {
                    SourcePath = imagePath,
                    TargetFolderPath = _viewModel.FolderPath,
                    FitMode = fitMode,
                    CropSelection = cropSelection
                }, CancellationToken.None);

                _viewModel.IconPath = result.OutputPath;
                await _viewModel.RefreshIconCacheButtonTextAsync();
                _toastService.Show(loc["ImageToIco_Toast_Success"], AccentToastBrush);
            }
            catch (Exception ex)
            {
                await _interruptDialogService.ShowFailureAsync(
                    loc["ImageToIco_Error_Title"],
                    loc["ImageToIco_Error_Headline"],
                    loc["ImageToIco_Error_Content"],
                    ex.ToString());
            }
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

        private static bool ShouldRefreshDragIntent(DragIntentResult immediateIntent)
        {
            return immediateIntent.Type is DragIntentType.ExeInternal or DragIntentType.ExeExternal;
        }

        private void ShowDragOverlay(DragIntentResult intent)
        {
            var loc = LocalizationManager.Instance;
            var mainText = loc[intent.MainTextKey];
            var subText = string.IsNullOrWhiteSpace(intent.SubTextKey) ? null : loc[intent.SubTextKey];
            var passiveChoiceCardCount = intent.Type == DragIntentType.ImageToIcon ? 3 : 0;
            var signature = $"{intent.Type}|{mainText}|{subText}|{intent.IconPath}|{intent.SubTextBrush}|{passiveChoiceCardCount}";
            if (string.Equals(_lastOverlaySignature, signature, StringComparison.Ordinal))
            {
                if (intent.Type == DragIntentType.ImageToIcon)
                {
                    _interruptDialogService.UpdatePassiveOverlayChoice(_imageDragSelectionKey);
                }
                return;
            }

            IReadOnlyList<DialogPassiveChoiceCardItem>? passiveChoiceCards = null;
            if (intent.Type == DragIntentType.ImageToIcon)
            {
                passiveChoiceCards = CreateImageDragChoiceCards(loc);
            }

            var ui = ConfigManager.Config.Ui;
            var isWarningMain = intent.Type == DragIntentType.Unsupported;
            var mainBrush = GetDragOverlayBrush(isWarningMain ? ui.DragOverlayWarningTextColor : ui.DragOverlayMainTextColor);
            var subBrush = GetDragOverlayBrush(string.IsNullOrWhiteSpace(intent.SubTextBrush) ? ui.DragOverlayWarningTextColor : intent.SubTextBrush);

            _interruptDialogService.ShowPassiveOverlay(new InterruptDialogOptions
            {
                Title = mainText,
                TitleForeground = mainBrush,
                CenterIconPath = intent.IconPath,
                SubText = subText,
                SubTextForeground = subBrush,
                WidthRatio = intent.Type == DragIntentType.ImageToIcon ? 1.2 : 1.0,
                ShowPrimaryButton = ConfigManager.Config.DragOverlay.ShowPrimaryButton,
                ShowSecondaryButton = ConfigManager.Config.DragOverlay.ShowSecondaryButton,
                DismissOnEsc = ConfigManager.Config.DragOverlay.DismissOnEsc,
                AllowOverlayClickDismiss = ConfigManager.Config.DragOverlay.AllowOverlayClickDismiss,
                PassiveChoiceCards = passiveChoiceCards,
                HitTestVisible = false
            });

            if (intent.Type == DragIntentType.ImageToIcon)
            {
                _imageDragSelectionKey = nameof(ImageFitMode.FillHeight);
                _interruptDialogService.UpdatePassiveOverlayChoice(_imageDragSelectionKey);
            }

            _componentFpsBadgeSource.SetDragOverlayVisible(true);
            _lastOverlaySignature = signature;
        }

        private static IReadOnlyList<DialogPassiveChoiceCardItem> CreateImageDragChoiceCards(LocalizationManager loc)
        {
            return
            [
                new DialogPassiveChoiceCardItem(nameof(ImageFitMode.FillHeight), loc["ImageToIco_Mode_FillHeight"], loc["ImageToIco_Mode_FillHeight_Desc"]),
                new DialogPassiveChoiceCardItem(nameof(ImageFitMode.FillWidth), loc["ImageToIco_Mode_FillWidth"], loc["ImageToIco_Mode_FillWidth_Desc"]),
                new DialogPassiveChoiceCardItem(nameof(ImageFitMode.ManualCrop), loc["ImageToIco_Mode_Manual"], loc["ImageToIco_Mode_Manual_Desc"])
            ];
        }

        private SolidColorBrush GetDragOverlayBrush(string colorText)
        {
            if (_dragOverlayBrushCache.TryGetValue(colorText, out var brush))
            {
                return brush;
            }

            brush = new SolidColorBrush(Color.Parse(colorText));
            _dragOverlayBrushCache[colorText] = brush;
            return brush;
        }

        private void ResetDragOverlayState()
        {
            _viewModel.IsDragOver = false;
            _animationStateSource.SetDragging(false);
            _animationStateSource.MarkStaticDirty();
            _lastDragIntent = DragIntentResult.Unsupported;
            _lastOverlaySignature = null;
            _imageDragSelectionKey = nameof(ImageFitMode.FillHeight);
            Interlocked.Increment(ref _dragIntentVersion);
            _dragIntentCts?.Cancel();
            _dragOverlayWatchdogTimer.Stop();
            _componentFpsBadgeSource.SetDragOverlayVisible(false);
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
            if (_lastDragIntent.Type == DragIntentType.ImageToIcon)
            {
                var selectedKey = _interruptDialogService.ResolvePassiveChoiceAt(position.X, position.Y, _imageDragSelectionKey);
                if (!string.IsNullOrWhiteSpace(selectedKey) && !string.Equals(_imageDragSelectionKey, selectedKey, StringComparison.Ordinal))
                {
                    _imageDragSelectionKey = selectedKey;
                    _interruptDialogService.UpdatePassiveOverlayChoice(selectedKey);
                }
            }
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

            _editView.SetAmbientSuspended(_isWindowRuntimeSuspended || DebugRuntimeAnalysis.PauseAnimations);
            
            _homeView.ZIndex = 0;
            _editView.ZIndex = 1;

            _homeView.IsVisible = false;
            _ = AnimateIn(_editView);
            _animationStateSource.MarkStaticDirty();
           
            var languageButton = this.FindControl<Button>("LanguageButton");
            if (languageButton != null) languageButton.IsVisible = false;
        }

        public void GoToHomeView()
        {
            if (_homeView == null || _editView == null) return;

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
            _animationStateSource.MarkStaticDirty();
           
            var languageButton = this.FindControl<Button>("LanguageButton");
            if (languageButton != null) languageButton.IsVisible = true;
        }

        private async Task AnimateIn(Control view)
        {
            _animationStateSource.MarkTransitionActivity(ViewTransitionDurationMs);
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
            _animationStateSource.MarkStaticDirty();
        }

        public async Task AnimateFadeIn()
        {
            _animationStateSource.MarkTransitionActivity(WindowTransitionDurationMs);
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
            _animationStateSource.MarkStaticDirty();
        }

        public async Task AnimateMinimizeTransitionAsync()
        {
            if (_rootLayer == null || WindowState == WindowState.Minimized)
            {
                return;
            }

            _animationStateSource.MarkTransitionActivity(WindowTransitionDurationMs);
            var token = ReplaceWindowRootAnimationToken();
            var translate = EnsureRootLayerTranslateTransform();
            _rootLayer.Opacity = 1;
            translate.X = 0;
            translate.Y = 0;

            var animation = new Animation
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
                            new Setter(Visual.OpacityProperty, 1.0),
                            new Setter(Avalonia.Media.TranslateTransform.YProperty, 0.0)
                        }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1),
                        Setters =
                        {
                            new Setter(Visual.OpacityProperty, 0.78),
                            new Setter(Avalonia.Media.TranslateTransform.YProperty, 14.0)
                        }
                    }
                }
            };

            try
            {
                await animation.RunAsync(_rootLayer, token);
            }
            catch (OperationCanceledException)
            {
            }
        }

        public void BeginMinimizeFromTitleBar()
        {
            if (WindowState == WindowState.Minimized)
            {
                return;
            }

            CancelWindowRootAnimation();
            ResetRootLayerPresentationState();
            DismissTransientPopupUi();
            WindowState = WindowState.Minimized;
        }

        private async Task AnimateRestoreTransitionAsync()
        {
            if (_rootLayer == null || _isWindowRuntimeSuspended)
            {
                return;
            }

            _animationStateSource.MarkTransitionActivity(WindowTransitionDurationMs);
            var token = ReplaceWindowRootAnimationToken();
            var translate = EnsureRootLayerTranslateTransform();
            _rootLayer.Opacity = 0.84;
            translate.X = 0;
            translate.Y = 14;

            var animation = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(220),
                Easing = new CubicEaseOut(),
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0),
                        Setters =
                        {
                            new Setter(Visual.OpacityProperty, 0.84),
                            new Setter(Avalonia.Media.TranslateTransform.YProperty, 14.0)
                        }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1),
                        Setters =
                        {
                            new Setter(Visual.OpacityProperty, 1.0),
                            new Setter(Avalonia.Media.TranslateTransform.YProperty, 0.0)
                        }
                    }
                }
            };

            try
            {
                await animation.RunAsync(_rootLayer, token);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (_rootLayer != null)
                {
                    _rootLayer.Opacity = 1;
                }

                translate.Y = 0;
            }
        }

        public async Task AnimateFadeOut()
        {
            _animationStateSource.MarkTransitionActivity(WindowTransitionDurationMs);
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
            _animationStateSource.MarkStaticDirty();
        }

        private async Task AnimateOut(Control view)
        {
            _animationStateSource.MarkTransitionActivity(ViewTransitionDurationMs);
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
            _animationStateSource.MarkStaticDirty();
        }
        
        private async void LanguageButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_languagePopup == null) return;
            e.Handled = true;
            var nextOpen = !_languagePopupDesiredOpen;
            if (!_languagePopup.IsOpen && _languagePopupAnimationCts == null)
            {
                nextOpen = true;
            }

            await SetLanguagePopupOpenAsync(nextOpen);
        }

        private async Task SetLanguagePopupOpenAsync(bool shouldOpen)
        {
            if (_languagePopup == null)
            {
                return;
            }

            _languagePopupDesiredOpen = shouldOpen;
            _languagePopupAnimationCts?.Cancel();
            _languagePopupAnimationCts?.Dispose();
            var cts = new CancellationTokenSource();
            _languagePopupAnimationCts = cts;
            var token = cts.Token;

            try
            {
                if (shouldOpen)
                {
                    await ShowLanguagePopupCoreAsync(token);
                }
                else
                {
                    await HideLanguagePopupCoreAsync(token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (ReferenceEquals(_languagePopupAnimationCts, cts))
                {
                    _languagePopupAnimationCts = null;
                }

                cts.Dispose();
            }
        }

        private async Task ShowLanguagePopupCoreAsync(CancellationToken token)
        {
            if (_languagePopup == null)
            {
                return;
            }

            _languagePopup.IsLightDismissEnabled = false;
            _animationStateSource.MarkTransitionActivity(PopupTransitionDurationMs);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            token.ThrowIfCancellationRequested();

            _languagePopup.IsOpen = true;
            token.ThrowIfCancellationRequested();

            if (_languagePopup.Child is { } popupContent)
            {
                var translate = popupContent.RenderTransform as Avalonia.Media.TranslateTransform ?? new Avalonia.Media.TranslateTransform();
                popupContent.RenderTransform = translate;
                popupContent.Opacity = 0;
                translate.Y = 10;
                var animation = new Animation
                {
                    Duration = TimeSpan.FromMilliseconds(300),
                    Easing = new CubicEaseOut(),
                    FillMode = FillMode.Forward,
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
                await animation.RunAsync(popupContent, token);
                popupContent.Opacity = 1;
                translate.Y = 0;
            }

            token.ThrowIfCancellationRequested();
            if (!IsActive)
            {
                _languagePopupDesiredOpen = false;
                _languagePopup.IsOpen = false;
                _languagePopup.IsLightDismissEnabled = true;
                _animationStateSource.MarkStaticDirty();
                return;
            }

            if (_languagePopupDesiredOpen)
            {
                _languagePopup.IsLightDismissEnabled = true;
            }

            _animationStateSource.MarkStaticDirty();
        }

        private async Task HideLanguagePopupCoreAsync(CancellationToken token)
        {
            if (_languagePopup == null)
            {
                return;
            }

            _languagePopup.IsLightDismissEnabled = false;
            _animationStateSource.MarkTransitionActivity(PopupTransitionDurationMs);
            if (_languagePopup.IsOpen && _languagePopup.Child is { } closingContent)
            {
                var translate = closingContent.RenderTransform as Avalonia.Media.TranslateTransform ?? new Avalonia.Media.TranslateTransform();
                closingContent.RenderTransform = translate;
                closingContent.Opacity = 1;
                translate.Y = 0;
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
                await closeAnimation.RunAsync(closingContent, token);
            }

            token.ThrowIfCancellationRequested();
            if (!_languagePopupDesiredOpen)
            {
                _languagePopup.IsOpen = false;
            }

            _animationStateSource.MarkStaticDirty();
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
        
        private void StartRenderLoop()
        {
            if (_isRenderLoopActive)
            {
                return;
            }

            _isRenderLoopActive = true;
            RequestRenderFrame();
        }

        private void RequestRenderFrame()
        {
            if (!_isRenderLoopActive || _isRenderFramePending)
            {
                return;
            }

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                return;
            }

            _isRenderFramePending = true;
            topLevel.RequestAnimationFrame(OnRenderFrame);
        }

        private void AnimationStateSource_StateChanged(object? sender, EventArgs e)
        {
            _renderWakeTimer.Stop();
            RequestRenderFrame();
        }

        private void RenderWakeTimer_Tick(object? sender, EventArgs e)
        {
            _renderWakeTimer.Stop();
            RequestRenderFrame();
        }

        private void IdleMemoryTrimTimer_Tick(object? sender, EventArgs e)
        {
            if (_isWindowRuntimeSuspended ||
                WindowState == WindowState.Minimized ||
                _frameRateSettings.CurrentForegroundTargetFps > 0 ||
                _viewModel.IsLoadingIcons ||
                _viewModel.IsLoadingIconsIndicatorVisible)
            {
                return;
            }

            _viewModel.RequestIdleMemoryTrim();
        }

        private void OnRenderFrame(TimeSpan time)
        {
            _isRenderFramePending = false;
            if (!_isRenderLoopActive)
            {
                return;
            }

            var snapshot = _animationStateSource.Snapshot();
            _frameRateGovernor.Update(snapshot, _displayInfoService.CurrentRefreshRateHz);
            UpdateTransparencyModeForCurrentFrame();
            var decision = _renderScheduler.Evaluate(
                _frameRateGovernor.ForegroundTargetFps,
                snapshot.HasStaticDirtyRegion,
                _frameRateSettings.StaticContentRefreshFps);

            if (decision.ShouldRenderForeground)
            {
                _performanceTelemetryService.RecordForegroundFrame();
                _layerInvalidationController.Invalidate(RenderLayer.Content);
                _layerInvalidationController.Invalidate(RenderLayer.Overlay);
            }

            if (decision.ShouldRenderStatic)
            {
                _performanceTelemetryService.RecordStaticFrame();
                _layerInvalidationController.Invalidate(RenderLayer.Static);
                _animationStateSource.ClearStaticDirty();
            }

            if (_frameRateGovernor.ForegroundTargetFps > 0)
            {
                RequestRenderFrame();
                return;
            }

            if (snapshot.HasStaticDirtyRegion && decision.NextWakeDelay is { } wakeDelay)
            {
                ScheduleRenderWake(wakeDelay);
            }
        }

        private void ScheduleRenderWake(TimeSpan delay)
        {
            var clampedDelay = delay <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(8) : delay;
            if (_renderWakeTimer.IsEnabled && _renderWakeTimer.Interval <= clampedDelay)
            {
                return;
            }

            _renderWakeTimer.Stop();
            _renderWakeTimer.Interval = clampedDelay;
            _renderWakeTimer.Start();
        }

        private void UpdateTransparencyModeForCurrentFrame()
        {
            var shouldUseLowCostTransparency = _frameRateGovernor.ForegroundTargetFps <= 0 || _isWindowRuntimeSuspended;
            if (_isLowCostTransparencyActive == shouldUseLowCostTransparency)
            {
                return;
            }

            _isLowCostTransparencyActive = shouldUseLowCostTransparency;
            Background = shouldUseLowCostTransparency ? LowCostWindowBackground : ActiveWindowBackground;
            this.TransparencyLevelHint = shouldUseLowCostTransparency
                ? new[] { WindowTransparencyLevel.None }
                : new[] { WindowTransparencyLevel.AcrylicBlur };
        }

        private void SetWindowRuntimeSuspended(bool suspended)
        {
            if (_isWindowRuntimeSuspended == suspended)
            {
                return;
            }

            _isWindowRuntimeSuspended = suspended;
            _performanceTelemetryService.SetSuspended(suspended);
            _performanceMonitorViewModel.SetHostSuspended(suspended);
            UpdateAmbientSuspensionState();

            if (suspended)
            {
                CancelWindowRootAnimation();
                ResetRootLayerPresentationState();
                DismissTransientPopupUi();
                if (_rootLayer != null)
                {
                    _rootLayer.IsVisible = false;
                }
                _renderWakeTimer.Stop();
                _idleMemoryTrimTimer.Stop();
                _isRenderLoopActive = false;
                _isRenderFramePending = false;
                _viewModel.RequestIdleMemoryTrim();
                Background = LowCostWindowBackground;
                TransparencyLevelHint = new[] { WindowTransparencyLevel.None };
                return;
            }

            if (_rootLayer != null)
            {
                _rootLayer.IsVisible = true;
            }
            RestoreWindowPresentationState();
            _idleMemoryTrimTimer.Start();
            StartRenderLoop();
            _animationStateSource.MarkStaticDirty();
            RequestRenderFrame();
        }

        private void RestoreWindowPresentationState()
        {
            Opacity = 1;
            if (RenderTransform is ScaleTransform scaleTransform)
            {
                scaleTransform.ScaleX = 1;
                scaleTransform.ScaleY = 1;
            }

            if (_rootLayer != null)
            {
                _rootLayer.IsVisible = true;
            }

            ResetRootLayerPresentationState();

            Background = _isLowCostTransparencyActive ? LowCostWindowBackground : ActiveWindowBackground;
        }

        private void ResetRootLayerPresentationState()
        {
            if (_rootLayer == null)
            {
                return;
            }

            _rootLayer.Opacity = 1;
            var translate = EnsureRootLayerTranslateTransform();
            translate.X = 0;
            translate.Y = 0;
        }

        private TranslateTransform EnsureRootLayerTranslateTransform()
        {
            if (_rootLayer?.RenderTransform is TranslateTransform translate)
            {
                return translate;
            }

            var created = new TranslateTransform();
            if (_rootLayer != null)
            {
                _rootLayer.RenderTransform = created;
            }

            return created;
        }

        private CancellationToken ReplaceWindowRootAnimationToken()
        {
            _windowRootAnimationCts?.Cancel();
            _windowRootAnimationCts?.Dispose();
            _windowRootAnimationCts = new CancellationTokenSource();
            return _windowRootAnimationCts.Token;
        }

        private void CancelWindowRootAnimation()
        {
            _windowRootAnimationCts?.Cancel();
            _windowRootAnimationCts?.Dispose();
            _windowRootAnimationCts = null;
        }

        private void DismissTransientPopupUi()
        {
            _performanceMonitor?.DismissContextMenu();

            if (_pinToolTipPopup != null)
            {
                _pinToolTipPopup.Opacity = 0;
                _pinToolTipPopup.IsOpen = false;
            }

            if (_langToolTipPopup != null)
            {
                _langToolTipPopup.Opacity = 0;
                _langToolTipPopup.IsOpen = false;
            }

            _languagePopupDesiredOpen = false;
            _languagePopupAnimationCts?.Cancel();
            if (_languagePopup != null)
            {
                _languagePopup.IsLightDismissEnabled = true;
                _languagePopup.IsOpen = false;
            }
        }

        private void DebugRuntimeAnalysis_PauseAnimationsChanged(object? sender, EventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                UpdateAmbientSuspensionState();
                _animationStateSource.MarkStaticDirty();
                RequestRenderFrame();
            }, DispatcherPriority.Background);
        }

        private void UpdateAmbientSuspensionState()
        {
            var ambientSuspended = _isWindowRuntimeSuspended || DebugRuntimeAnalysis.PauseAnimations;
            _backgroundAmbientHandle?.SetEnabled(!ambientSuspended);
            _pinGlowAmbientHandle?.SetEnabled(!ambientSuspended && IsPinnedVisualActive && !_frameRateSettings.ExcludePinGlow);
            _homeView?.SetAmbientSuspended(ambientSuspended);
            _editView?.SetAmbientSuspended(ambientSuspended);
        }

        private void TickBackgroundAmbientFlow(double nowSeconds)
        {
            if (_flowLayer == null || !IsVisible)
            {
                return;
            }

            if (!EnsureBackgroundFlowBrush())
            {
                return;
            }

            var progress = nowSeconds % 16.0 / 16.0;
            if (progress > 0.5)
            {
                progress = 1.0 - progress;
            }

            progress *= 2.0;
            _backgroundFlowPhase = progress;

            var startColor1 = Color.FromArgb(0x24, 0xFF, 0x74, 0x74);
            var endColor1 = Color.FromArgb(0x1F, 0x74, 0xA1, 0xFF);
            var startColor2 = Color.FromArgb(0x2E, 0x74, 0xFF, 0xC7);
            var endColor2 = Color.FromArgb(0x2E, 0xFF, 0xCB, 0x74);

            _backgroundFlowBrush!.StartPoint = new RelativePoint(_backgroundFlowPhase, 0, RelativeUnit.Relative);
            _backgroundFlowBrush.EndPoint = new RelativePoint(1 - _backgroundFlowPhase, 1, RelativeUnit.Relative);
            _backgroundFlowStartStop!.Color = LerpColor(startColor1, startColor2, _backgroundFlowPhase);
            _backgroundFlowEndStop!.Color = LerpColor(endColor1, endColor2, _backgroundFlowPhase);
            _layerInvalidationController.Invalidate(RenderLayer.Ambient);
        }

        private void TickPinGlowAmbient(double nowSeconds)
        {
            if (!IsPinnedVisualActive || _pinIconGlow == null || _isWindowRuntimeSuspended || !IsVisible || _frameRateSettings.ExcludePinGlow)
            {
                return;
            }

            _pinGlowPhase = nowSeconds * 2.05;
            ApplyPinGlowFrame();
        }

        private bool EnsureBackgroundFlowBrush()
        {
            if (_flowLayer == null)
            {
                return false;
            }

            if (_backgroundFlowBrush != null && _backgroundFlowStartStop != null && _backgroundFlowEndStop != null)
            {
                return true;
            }

            if (_flowLayer.Background is LinearGradientBrush existingBrush && existingBrush.GradientStops.Count >= 2)
            {
                _backgroundFlowBrush = existingBrush;
                _backgroundFlowStartStop = existingBrush.GradientStops[0];
                _backgroundFlowEndStop = existingBrush.GradientStops[1];
                return true;
            }

            _backgroundFlowStartStop = new GradientStop(Color.FromArgb(0x24, 0xFF, 0x74, 0x74), 0);
            _backgroundFlowEndStop = new GradientStop(Color.FromArgb(0x1F, 0x74, 0xA1, 0xFF), 1);
            _backgroundFlowBrush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    _backgroundFlowStartStop,
                    _backgroundFlowEndStop
                }
            };
            _flowLayer.Background = _backgroundFlowBrush;
            return true;
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
            var isPinned = IsPinnedVisualActive;

            if (_pinButton != null)
            {
                _pinButton.Background = isPinned ? PinButtonPinnedBackgroundBrush : PinButtonDefaultBackgroundBrush;
                _pinButton.Classes.Set("pinned", isPinned);
            }

            if (_languageButton != null)
            {
                _languageButton.Background = PinButtonDefaultBackgroundBrush;
            }

            _pinButtonIcon.Opacity = isPinned ? 0.82 : 0.4;
            UpdatePinGlowVisual();
        }

        private void ToggleTopmost()
        {
            if (_frameRateSettings.ExcludeActualTopmost)
            {
                _debugPinnedVisualState = !_debugPinnedVisualState;
            }
            else
            {
                this.Topmost = !this.Topmost;
            }

            UpdatePinButtonIcon();

            var toastService = App.Services!.GetRequiredService<IToastService>();
            var message = IsPinnedVisualActive
                ? LocalizationManager.Instance["Toast_WindowPinned"]
                : LocalizationManager.Instance["Toast_WindowUnpinned"];
            toastService.Show(message, AccentToastBrush);
        }

        private void UpdatePinGlowVisual()
        {
            if (_pinIconGlow == null)
            {
                return;
            }

            if (IsPinnedVisualActive && !_frameRateSettings.ExcludePinGlow)
            {
                _pinGlowAmbientHandle?.SetEnabled(!_isWindowRuntimeSuspended);
                _pinGlowPhase = 0;
                ApplyPinGlowFrame();
                return;
            }

            _pinGlowAmbientHandle?.SetEnabled(false);
            _pinGlowPhase = 0;
            _pinIconGlow.Opacity = 0;
        }

        private void ApplyPinGlowFrame()
        {
            if (_pinIconGlow == null)
            {
                return;
            }

            var pulse = (Math.Sin(_pinGlowPhase) + 1) * 0.5;
            _pinIconGlow.Opacity = 0.2 + pulse * 0.24;
            if (_pinGlowScaleTransform != null)
            {
                var scale = 0.9 + pulse * 0.2;
                _pinGlowScaleTransform.ScaleX = scale;
                _pinGlowScaleTransform.ScaleY = scale;
            }
        }

        private void MainWindow_TunnelPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (TryHandleDebugComponentExcludeGesture(e))
            {
                _pendingWindowTapCandidate = false;
                _dragStartedForCurrentPress = false;
                _pressEventForMoveDrag = null;
            }
        }

        private bool IsWindowTapInteractiveSource(Control? source)
        {
            if (source == null)
            {
                return false;
            }

            return source.FindAncestorOfType<TextBox>() != null ||
                   source.FindAncestorOfType<ScrollBar>() != null ||
                   source.FindAncestorOfType<Thumb>() != null ||
                   source.FindAncestorOfType<ToggleButton>() != null ||
                   source.FindAncestorOfType<CheckBox>() != null ||
                   source.FindAncestorOfType<Button>() != null ||
                   source.FindAncestorOfType<Popup>() != null ||
                   source.FindAncestorOfType<PerformanceMonitor>() != null ||
                   FindDebugExcludedPlaceholder(source) != null;
        }

        private bool TryHandleDebugComponentExcludeGesture(PointerPressedEventArgs e)
        {
            if (!_frameRateSettings.EnableComponentExcludeMode ||
                !e.KeyModifiers.HasFlag(KeyModifiers.Control) ||
                !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return false;
            }

            if (e.Source is not Control source)
            {
                return false;
            }

            if (source.FindAncestorOfType<InterruptDialog>() != null ||
                source.FindAncestorOfType<PerformanceMonitor>() != null ||
                source.FindAncestorOfType<Popup>() != null)
            {
                return false;
            }

            if (FindDebugExcludedPlaceholder(source) is { } placeholder)
            {
                RestoreDebugExcludedComponent(placeholder);
                e.Handled = true;
                return true;
            }

            var target = FindExcludableControl(source);
            if (target == null)
            {
                return false;
            }

            if (_debugExcludedComponents.ContainsKey(target))
            {
                RestoreDebugExcludedComponent(target);
                e.Handled = true;
                return true;
            }

            ExcludeDebugComponent(target);
            e.Handled = true;
            return true;
        }

        private Control? FindExcludableControl(Control source)
        {
            Control? current = source;
            while (current != null)
            {
                if (current is HomeView ||
                    current is EditView ||
                    current is InterruptDialog ||
                    current is PerformanceMonitor)
                {
                    current = current.GetVisualParent() as Control;
                    continue;
                }

                if (current.Parent is Panel && current is not TextBlock)
                {
                    return current;
                }

                current = current.GetVisualParent() as Control;
            }

            return null;
        }

        private Border? FindDebugExcludedPlaceholder(Control source)
        {
            Control? current = source;
            while (current != null)
            {
                if (current is Border border && border.Classes.Contains("DebugExcludedPlaceholder"))
                {
                    return border;
                }

                current = current.GetVisualParent() as Control;
            }

            return null;
        }

        private void ExcludeDebugComponent(Control target)
        {
            if (target.Parent is not Panel parentPanel)
            {
                return;
            }

            var childIndex = parentPanel.Children.IndexOf(target);
            if (childIndex < 0)
            {
                return;
            }

            var placeholder = CreateDebugExcludedPlaceholder(target);
            CopyLayoutProperties(target, placeholder);

            var state = new DebugExcludedComponentState
            {
                OriginalControl = target,
                PlaceholderControl = placeholder,
                ParentPanel = parentPanel,
                ChildIndex = childIndex
            };

            parentPanel.Children.RemoveAt(childIndex);
            parentPanel.Children.Insert(childIndex, placeholder);
            _debugExcludedComponents[target] = state;
            _debugExcludedPlaceholders[placeholder] = state;
            _animationStateSource.MarkStaticDirty();
        }

        private Border CreateDebugExcludedPlaceholder(Control target)
        {
            var placeholder = new Border
            {
                Classes = { "DebugExcludedPlaceholder" },
                Background = DebugExcludedPlaceholderBackgroundBrush,
                BorderBrush = DebugExcludedPlaceholderBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 10),
                MinHeight = Math.Max(40, target.Bounds.Height > 0 ? target.Bounds.Height : 40),
                Child = new TextBlock
                {
                    Text = LocalizationManager.Instance["Dialog_FrameRate_ComponentExcluded"],
                    Foreground = DebugExcludedPlaceholderForegroundBrush,
                    FontWeight = FontWeight.SemiBold,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                }
            };
            placeholder.PointerPressed += DebugExcludedPlaceholder_PointerPressed;
            return placeholder;
        }

        private void DebugExcludedPlaceholder_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Control) ||
                !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            if (sender is Border placeholder)
            {
                RestoreDebugExcludedComponent(placeholder);
                e.Handled = true;
            }
        }

        private void RestoreDebugExcludedComponent(Control targetOrPlaceholder)
        {
            DebugExcludedComponentState? state = null;
            if (_debugExcludedComponents.TryGetValue(targetOrPlaceholder, out var byComponent))
            {
                state = byComponent;
            }
            else if (_debugExcludedPlaceholders.TryGetValue(targetOrPlaceholder, out var byPlaceholder))
            {
                state = byPlaceholder;
            }

            if (state == null)
            {
                return;
            }

            state.PlaceholderControl.PointerPressed -= DebugExcludedPlaceholder_PointerPressed;
            var currentIndex = state.ParentPanel.Children.IndexOf(state.PlaceholderControl);
            if (currentIndex >= 0)
            {
                state.ParentPanel.Children.RemoveAt(currentIndex);
                state.ParentPanel.Children.Insert(currentIndex, state.OriginalControl);
            }
            else
            {
                state.ParentPanel.Children.Insert(Math.Min(state.ChildIndex, state.ParentPanel.Children.Count), state.OriginalControl);
            }

            _debugExcludedComponents.Remove(state.OriginalControl);
            _debugExcludedPlaceholders.Remove(state.PlaceholderControl);
            _animationStateSource.MarkStaticDirty();
        }

        private void RestoreAllDebugExcludedComponents()
        {
            foreach (var state in new List<DebugExcludedComponentState>(_debugExcludedComponents.Values))
            {
                RestoreDebugExcludedComponent(state.OriginalControl);
            }
        }

        private static void CopyLayoutProperties(Control source, Control target)
        {
            target.Margin = source.Margin;
            target.Width = source.Width;
            target.Height = source.Height;
            target.MinWidth = source.MinWidth;
            target.MinHeight = source.MinHeight;
            target.MaxWidth = source.MaxWidth;
            target.MaxHeight = source.MaxHeight;
            target.HorizontalAlignment = source.HorizontalAlignment;
            target.VerticalAlignment = source.VerticalAlignment;

            Grid.SetRow(target, Grid.GetRow(source));
            Grid.SetColumn(target, Grid.GetColumn(source));
            Grid.SetRowSpan(target, Grid.GetRowSpan(source));
            Grid.SetColumnSpan(target, Grid.GetColumnSpan(source));
            DockPanel.SetDock(target, DockPanel.GetDock(source));
            Canvas.SetLeft(target, Canvas.GetLeft(source));
            Canvas.SetTop(target, Canvas.GetTop(source));
            Canvas.SetRight(target, Canvas.GetRight(source));
            Canvas.SetBottom(target, Canvas.GetBottom(source));
        }

    }
}
