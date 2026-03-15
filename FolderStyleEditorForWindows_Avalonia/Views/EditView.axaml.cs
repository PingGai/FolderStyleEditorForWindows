using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;
using Avalonia.Xaml.Interactions.Animated;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;
using System.Windows.Input;
using FolderStyleEditorForWindows;
using FolderStyleEditorForWindows.Services;
using FolderStyleEditorForWindows.Models;
using Microsoft.Extensions.DependencyInjection;

namespace FolderStyleEditorForWindows.Views
{
    public partial class EditView : UserControl, IDisposable
    {
        private sealed class EditDragTipItem
        {
            public EditDragTipItem(string key, string text, Color startColor, Color endColor)
            {
                Key = key;
                Text = text;
                StartColor = startColor;
                EndColor = endColor;
            }

            public string Key { get; }
            public string Text { get; }
            public Color StartColor { get; }
            public Color EndColor { get; }
        }

        private const int TitleGradientCycles = 3;
        private bool _suppressAliasAutocompleteDismissOnce;
        private readonly InterruptibleScalarAnimator _iconListScrollAnimator;
        private readonly DispatcherTimer _iconCounterRepeatStartTimer;
        private readonly DispatcherTimer _iconCounterRepeatTickTimer;
        private readonly DispatcherTimer _iconCounterMiddleAutoScrollTimer;
        private readonly AmbientAnimationScheduler? _ambientScheduler;
        private readonly FrameRateSettings? _frameRateSettings;
        private IAmbientAnimationHandle? _adminTitleAmbientHandle;
        private IAmbientAnimationHandle? _dragTipAmbientHandle;
        private ScrollViewer? _editScrollViewer;
        private ScrollViewer? _iconListScrollViewer;
        private ScrollViewer? _aliasAutocompleteScrollViewer;
        private VerticalScrollViewerAnimatedBehavior? _editScrollAnimatedBehavior;
        private VerticalScrollViewerAnimatedBehavior? _iconListScrollAnimatedBehavior;
        private VerticalScrollViewerAnimatedBehavior? _aliasAutocompleteScrollAnimatedBehavior;
        private double _iconListTargetOffsetY;
        private int _iconCounterRepeatDelta;
        private Key? _iconCounterRepeatKey;
        private ViewModels.MainViewModel? _iconCounterRepeatViewModel;
        private double _iconCounterMiddleAnchorY;
        private double _iconCounterMiddleCurrentY;
        private double _iconCounterMiddleScrollRemainder;
        private ViewModels.MainViewModel? _iconCounterMiddleScrollViewModel;
        private IPointer? _iconCounterCapturedPointer;
        private bool _isIconCounterMiddleAdjustActive;
        private Window? _hostWindow;
        private int _iconCounterMiddleLastDirection;
        private bool _pendingInitialScrollReset;
        private LinearGradientBrush? _titleGoldBrush;
        private TextBlock? _editTitleText;
        private double _titleGradientPhase;
        private double _lastTitleAmbientTickSeconds;
        private bool _isAdminTitleAmbientEnabled;
        private bool _isAmbientSuspended;
        private ElevationSessionState? _elevationSessionState;
        private LocalizationManager? _localizationManager;
        private Grid? _editDragTipSlot;
        private TextBlock? _editDragTipLeadText;
        private TextBlock? _editDragTipTrailText;
        private TextBlock? _editDragTipCurrentText;
        private TextBlock? _editDragTipCurrentGhostText;
        private TextBlock? _editDragTipNextText;
        private TextBlock? _editDragTipNextGhostText;
        private LinearGradientBrush? _editDragTipCurrentBrush;
        private LinearGradientBrush? _editDragTipNextBrush;
        private BlurEffect? _editDragTipLeadBlur;
        private BlurEffect? _editDragTipTrailBlur;
        private TranslateTransform? _editDragTipCurrentTranslate;
        private TranslateTransform? _editDragTipCurrentGhostTranslate;
        private TranslateTransform? _editDragTipNextTranslate;
        private TranslateTransform? _editDragTipNextGhostTranslate;
        private ScaleTransform? _editDragTipCurrentScale;
        private ScaleTransform? _editDragTipNextScale;
        private SkewTransform? _editDragTipCurrentSkew;
        private SkewTransform? _editDragTipNextSkew;
        private BlurEffect? _editDragTipCurrentBlur;
        private BlurEffect? _editDragTipCurrentGhostBlur;
        private BlurEffect? _editDragTipNextBlur;
        private BlurEffect? _editDragTipNextGhostBlur;
        private readonly List<EditDragTipItem> _editDragTipItems = new();
        private int _editDragTipCurrentIndex;
        private int _editDragTipNextIndex = -1;
        private double _editDragTipElapsedSeconds;
        private double _lastDragTipAmbientTickSeconds;
        private double _editDragTipAnimationProgress;
        private bool _isDragTipAnimating;
        private double _editDragTipRotationIntervalSeconds = 3.0;
        private const double DragTipAnimationDurationSeconds = 0.78;
        private const double DragTipTravelDistance = 18.0;
        private readonly Color[] _titleGradientColors =
        {
            Color.Parse("#FFB347"),
            Color.Parse("#FFD56A"),
            Color.Parse("#F3DF84"),
            Color.Parse("#E0A93F"),
            Color.Parse("#FFD07A")
        };

        public EditView()
        {
            InitializeComponent();

            _iconListScrollAnimator = new InterruptibleScalarAnimator(
                () => _iconListScrollViewer?.Offset.Y ?? _iconListTargetOffsetY,
                value =>
                {
                    if (_iconListScrollViewer == null)
                    {
                        return;
                    }

                    var currentOffset = _iconListScrollViewer.Offset;
                    if (Math.Abs(currentOffset.Y - value) < 0.1)
                    {
                        return;
                    }

                    _iconListScrollViewer.Offset = new Vector(currentOffset.X, value);
                });
            _iconCounterRepeatStartTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(360)
            };
            _iconCounterRepeatStartTimer.Tick += IconCounterRepeatStartTimer_Tick;
            _iconCounterRepeatTickTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(72)
            };
            _iconCounterRepeatTickTimer.Tick += IconCounterRepeatTickTimer_Tick;
            _iconCounterMiddleAutoScrollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _iconCounterMiddleAutoScrollTimer.Tick += IconCounterMiddleAutoScrollTimer_Tick;
            _ambientScheduler = App.Services?.GetService<AmbientAnimationScheduler>();
            _frameRateSettings = App.Services?.GetService<FrameRateSettings>();
            if (_ambientScheduler != null)
            {
                _adminTitleAmbientHandle = _ambientScheduler.Register(
                    "edit-admin-title-gradient",
                    () => _frameRateSettings?.AdminTitleAmbientFps ?? 15,
                    OnAdminTitleAmbientTick);
                _adminTitleAmbientHandle.SetEnabled(false);
                _dragTipAmbientHandle = _ambientScheduler.Register(
                    "edit-drag-tip-carousel",
                    () => _frameRateSettings?.EditHintCarouselFps ?? 16,
                    OnDragTipAmbientTick);
                _dragTipAmbientHandle.SetEnabled(false);
            }

            DebugRuntimeAnalysis.PauseAnimationsChanged += DebugRuntimeAnalysis_PauseAnimationsChanged;

            if (_frameRateSettings != null)
            {
                _frameRateSettings.PropertyChanged += FrameRateSettings_PropertyChanged;
            }
            UpdateIconListScrollAnimatorFrameRate();
        }

        public void Dispose()
        {
            UnsubscribeFromViewModel();
            StopIconCounterMiddleAdjustMode();
            _adminTitleAmbientHandle?.Dispose();
            _adminTitleAmbientHandle = null;
            _dragTipAmbientHandle?.Dispose();
            _dragTipAmbientHandle = null;
            _iconListScrollAnimator.Dispose();
            DebugRuntimeAnalysis.PauseAnimationsChanged -= DebugRuntimeAnalysis_PauseAnimationsChanged;
            if (_frameRateSettings != null)
            {
                _frameRateSettings.PropertyChanged -= FrameRateSettings_PropertyChanged;
            }
            if (_localizationManager != null)
            {
                _localizationManager.PropertyChanged -= LocalizationManager_PropertyChanged;
            }

            if (DataContext is ViewModels.MainViewModel vm)
            {
                vm.ClearIconPreview();
            }

            DetachIconListInfrastructure();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape)
            {
                if (VisualRoot is MainWindow mainWindow)
                {
                    mainWindow.GoToHomeView();
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Z && e.KeyModifiers == KeyModifiers.Control)
            {
                if (DataContext is ViewModels.MainViewModel vm)
                {
                    vm.UndoLastChange();
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.S && e.KeyModifiers == KeyModifiers.Control)
            {
                if (DataContext is ViewModels.MainViewModel vm && vm.SaveCommand.CanExecute(null))
                    vm.SaveCommand.Execute(null);
            }
        }

        protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _pendingInitialScrollReset = true;
            SubscribeToViewModel();

            var adminTitleBadge = this.FindControl<Control>("AdminTitlePerformanceBadge");
            if (adminTitleBadge != null)
            {
                adminTitleBadge.DataContext = App.Services?.GetService<ComponentFpsBadgeSource>();
            }

            var btnPickDir = this.FindControl<Button>("btnPickDir");
            if (btnPickDir != null)
            {
                btnPickDir.Click += BtnPickDir_Click;
            }

            var btnPickIcon = this.FindControl<Button>("btnPickIcon");
            if (btnPickIcon != null)
            {
                btnPickIcon.Click += BtnPickIcon_Click;
            }

            var btnOpenExplorer = this.FindControl<Button>("btnOpenExplorer");
            if (btnOpenExplorer != null)
            {
                btnOpenExplorer.Click += BtnOpenExplorer_Click;
            }
            var editTitleButton = this.FindControl<Button>("EditTitleButton");
            if (editTitleButton != null)
            {
                editTitleButton.Click += EditTitleButton_Click;
            }
            _editTitleText = this.FindControl<TextBlock>("EditTitleText");
            if (Resources.TryGetResource("EditTitleGoldAnimatedBrush", null, out var titleBrush) && titleBrush is LinearGradientBrush brush)
            {
                _titleGoldBrush = brush;
            }
            _editDragTipCurrentText = this.FindControl<TextBlock>("EditDragTipCurrentText");
            _editDragTipCurrentGhostText = this.FindControl<TextBlock>("EditDragTipCurrentGhostText");
            _editDragTipNextText = this.FindControl<TextBlock>("EditDragTipNextText");
            _editDragTipNextGhostText = this.FindControl<TextBlock>("EditDragTipNextGhostText");
            _editDragTipSlot = this.FindControl<Grid>("EditDragTipSlot");
            _editDragTipLeadText = this.FindControl<TextBlock>("EditDragTipLeadText");
            _editDragTipTrailText = this.FindControl<TextBlock>("EditDragTipTrailText");
            if (Resources.TryGetResource("EditDragTipCurrentBrush", null, out var currentBrush) && currentBrush is LinearGradientBrush currentLinearBrush)
            {
                _editDragTipCurrentBrush = currentLinearBrush;
            }
            if (Resources.TryGetResource("EditDragTipNextBrush", null, out var nextBrush) && nextBrush is LinearGradientBrush nextLinearBrush)
            {
                _editDragTipNextBrush = nextLinearBrush;
            }
            EnsureDragTipTransforms();
            _localizationManager = LocalizationManager.Instance;
            if (_localizationManager != null)
            {
                _localizationManager.PropertyChanged -= LocalizationManager_PropertyChanged;
                _localizationManager.PropertyChanged += LocalizationManager_PropertyChanged;
            }
            ReloadEditDragTipItems(resetIndex: true);
            _elevationSessionState = App.Services?.GetRequiredService<ElevationSessionState>();
            if (_elevationSessionState != null)
            {
                _elevationSessionState.PropertyChanged += ElevationSessionState_PropertyChanged;
                ApplyEditTitleStyle(_elevationSessionState.IsElevatedSessionActive);
            }

            var aliasInput = this.FindControl<TextBox>("aliasInput");
            if (aliasInput != null)
            {
                aliasInput.GotFocus += AliasInput_GotFocus;
                aliasInput.LostFocus += AliasInput_LostFocus;
                aliasInput.KeyDown += AliasInput_KeyDown;
                aliasInput.TextChanged += AliasInput_TextChanged;

                // Add drag and drop support for alias input
                aliasInput.AddHandler(DragDrop.DragOverEvent, AliasInput_DragOver);
                aliasInput.AddHandler(DragDrop.DropEvent, AliasInput_Drop);
            }
            var aliasAutocompletePopup = this.FindControl<Popup>("aliasAutocompletePopup");
            if (aliasAutocompletePopup != null && aliasInput != null)
            {
                aliasAutocompletePopup.PlacementTarget = aliasInput;
            }
            
            var iconInput = this.FindControl<TextBox>("iconInput");
            if (iconInput != null)
            {
                iconInput.LostFocus += IconInput_LostFocus;
            }

            var iconCounterDisplay = this.FindControl<TextBlock>("iconCounterDisplay");
            if (iconCounterDisplay != null)
            {
                iconCounterDisplay.PointerWheelChanged += IconCounterDisplay_PointerWheelChanged;
                iconCounterDisplay.PointerPressed += IconCounterDisplay_PointerPressed;
                iconCounterDisplay.PointerEntered += IconCounterDisplay_PointerEntered;
                iconCounterDisplay.PointerExited += IconCounterDisplay_PointerExited;
            }

            var iconCounterHost = this.FindControl<Border>("iconCounterHost");
            if (iconCounterHost != null)
            {
                iconCounterHost.AddHandler(PointerWheelChangedEvent, IconCounterDisplay_PointerWheelChanged, RoutingStrategies.Tunnel, handledEventsToo: true);
                iconCounterHost.PointerPressed += IconCounterHost_PointerPressed;
                iconCounterHost.PointerMoved += IconCounterHost_PointerMoved;
                iconCounterHost.PointerExited += IconCounterHost_PointerExited;
                iconCounterHost.PointerReleased += IconCounterHost_PointerReleased;
                EnsureTooltipAlwaysShows(iconCounterHost);
            }

            var iconCounterInput = this.FindControl<TextBox>("iconCounterInput");
            if (iconCounterInput != null)
            {
                iconCounterInput.PointerWheelChanged += IconCounterDisplay_PointerWheelChanged;
                iconCounterInput.KeyDown += IconCounterInput_KeyDown;
                iconCounterInput.KeyUp += IconCounterInput_KeyUp;
                iconCounterInput.LostFocus += IconCounterInput_LostFocus;
            }

            var editScrollViewer = this.FindControl<ScrollViewer>("editScrollViewer");
            _editScrollViewer = editScrollViewer;
            if (editScrollViewer != null && iconCounterHost != null)
            {
                editScrollViewer.AddHandler(PointerWheelChangedEvent, (sender, e) =>
                {
                    if (iconCounterHost.IsPointerOver)
                    {
                        e.Handled = true;
                    }
                }, RoutingStrategies.Tunnel, handledEventsToo: true);
                editScrollViewer.RemoveHandler(RequestBringIntoViewEvent, EditScrollViewer_RequestBringIntoView);
                editScrollViewer.AddHandler(RequestBringIntoViewEvent, EditScrollViewer_RequestBringIntoView, RoutingStrategies.Bubble, handledEventsToo: true);
            }

            _aliasAutocompleteScrollViewer = this.FindControl<ScrollViewer>("aliasAutocompleteScrollViewer");

            if (_aliasAutocompleteScrollViewer != null)
            {
                _aliasAutocompleteScrollViewer.PointerWheelChanged -= AliasAutocompleteScrollViewer_PointerWheelChanged;
                _aliasAutocompleteScrollViewer.PointerWheelChanged += AliasAutocompleteScrollViewer_PointerWheelChanged;
            }

            Dispatcher.UIThread.Post(AttachIconListInfrastructure, DispatcherPriority.Loaded);
            AttachWindowLifecycleHandlers();
            AddHandler(InputElement.PointerPressedEvent, EditView_PointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            AddHandler(InputElement.PointerMovedEvent, EditView_PointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);

            ResetEditScrollToTop();
            Dispatcher.UIThread.Post(ResetEditScrollToTop, DispatcherPriority.Loaded);
            ApplyScrollAnimationDebugState();
            UpdateAdminAmbientState();
            UpdateDragTipAmbientState();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            UnsubscribeFromViewModel();
            RemoveHandler(InputElement.PointerPressedEvent, EditView_PointerPressed);
            RemoveHandler(InputElement.PointerMovedEvent, EditView_PointerMoved);
            DetachWindowLifecycleHandlers();
            StopIconCounterMiddleAdjustMode();
            DetachIconListInfrastructure();
            if (_elevationSessionState != null)
            {
                _elevationSessionState.PropertyChanged -= ElevationSessionState_PropertyChanged;
            }
            _adminTitleAmbientHandle?.SetEnabled(false);
            _dragTipAmbientHandle?.SetEnabled(false);
            base.OnDetachedFromVisualTree(e);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == IsVisibleProperty)
            {
                UpdateAdminAmbientState();
                UpdateDragTipAmbientState();
            }
        }

        public void SetAmbientSuspended(bool suspended)
        {
            if (_isAmbientSuspended == suspended)
            {
                return;
            }

            _isAmbientSuspended = suspended;
            UpdateAdminAmbientState();
            UpdateDragTipAmbientState();
        }

        private void FrameRateSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FrameRateSettings.DisableEditScrollAnimations))
            {
                Dispatcher.UIThread.Post(ApplyScrollAnimationDebugState, DispatcherPriority.Background);
            }
            else if (e.PropertyName == nameof(FrameRateSettings.ActiveInteractionFps) ||
                     e.PropertyName == nameof(FrameRateSettings.DisplayRefreshRateHz))
            {
                Dispatcher.UIThread.Post(UpdateIconListScrollAnimatorFrameRate, DispatcherPriority.Background);
            }
            else if (e.PropertyName == nameof(FrameRateSettings.EditHintCarouselFps))
            {
                Dispatcher.UIThread.Post(UpdateDragTipAmbientState, DispatcherPriority.Background);
            }
        }

        private void ApplyScrollAnimationDebugState()
        {
            var enableAnimatedScroll = !(_frameRateSettings?.DisableEditScrollAnimations ?? false);
            SetAnimatedScrollBehavior(_editScrollViewer, ref _editScrollAnimatedBehavior, enableAnimatedScroll);
            SetAnimatedScrollBehavior(_iconListScrollViewer, ref _iconListScrollAnimatedBehavior, enableAnimatedScroll);
            SetAnimatedScrollBehavior(_aliasAutocompleteScrollViewer, ref _aliasAutocompleteScrollAnimatedBehavior, enableAnimatedScroll);
        }

        private static void SetAnimatedScrollBehavior(
            ScrollViewer? scrollViewer,
            ref VerticalScrollViewerAnimatedBehavior? cachedBehavior,
            bool enabled)
        {
            if (scrollViewer == null)
            {
                return;
            }

            var behaviors = Interaction.GetBehaviors(scrollViewer);
            var existingBehaviors = behaviors.OfType<VerticalScrollViewerAnimatedBehavior>().ToList();

            if (!enabled)
            {
                foreach (var existing in existingBehaviors)
                {
                    behaviors.Remove(existing);
                }

                if (cachedBehavior == null && existingBehaviors.Count > 0)
                {
                    cachedBehavior = existingBehaviors[0];
                }

                return;
            }

            cachedBehavior ??= existingBehaviors.FirstOrDefault() ?? new VerticalScrollViewerAnimatedBehavior();
            if (!behaviors.Contains(cachedBehavior))
            {
                behaviors.Add(cachedBehavior);
            }
        }

        private void AttachIconListInfrastructure()
        {
            var iconRowListBox = this.FindControl<ListBox>("iconRowListBox");
            _iconListScrollViewer = iconRowListBox?
                .GetVisualDescendants()
                .OfType<ScrollViewer>()
                .FirstOrDefault();

            if (_iconListScrollViewer != null)
            {
                _iconListScrollViewer.PointerWheelChanged -= IconListScrollViewer_PointerWheelChanged;
                _iconListScrollViewer.PointerWheelChanged += IconListScrollViewer_PointerWheelChanged;
            }

            if (iconRowListBox != null)
            {
                iconRowListBox.RemoveHandler(InputElement.KeyDownEvent, IconListBox_KeyDown);
                iconRowListBox.AddHandler(InputElement.KeyDownEvent, IconListBox_KeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
                iconRowListBox.PointerPressed -= IconListBox_PointerPressed;
                iconRowListBox.PointerPressed += IconListBox_PointerPressed;
                iconRowListBox.SelectedIndex = -1;
            }

            ApplyScrollAnimationDebugState();
        }

        private void DetachIconListInfrastructure()
        {
            var iconRowListBox = this.FindControl<ListBox>("iconRowListBox");
            if (iconRowListBox != null)
            {
                iconRowListBox.RemoveHandler(InputElement.KeyDownEvent, IconListBox_KeyDown);
                iconRowListBox.PointerPressed -= IconListBox_PointerPressed;
            }

            if (_iconListScrollViewer != null)
            {
                _iconListScrollViewer.PointerWheelChanged -= IconListScrollViewer_PointerWheelChanged;
                _iconListScrollViewer = null;
            }
        }

        private void AliasInput_GotFocus(object? sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm && vm.IsAliasAsPlaceholder)
            {
                vm.IsAliasAsPlaceholder = false;
            }
        }

        private void AliasInput_LostFocus(object? sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                if (_suppressAliasAutocompleteDismissOnce)
                {
                    _suppressAliasAutocompleteDismissOnce = false;
                    return;
                }
                var popup = this.FindControl<Popup>("aliasAutocompletePopup");
                if (popup?.IsOpen == true && popup.Child?.IsPointerOver == true)
                {
                    return;
                }
                vm.DismissAliasAutocomplete();
                vm.RestoreDefaultAliasIfNeeded();
            }
        }

        private void IconInput_LostFocus(object? sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                vm.RestoreDefaultIconIfNeeded();
            }
        }

        private void IconCounterDisplay_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                var delta = e.Delta.Y > 0 ? -1 : 1;
                vm.MoveIconIndex(delta, wrap: true);
                e.Handled = true;
            }
        }

        private void IconCounterDisplay_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            if (DataContext is ViewModels.MainViewModel vm)
            {
                StopIconCounterMiddleAdjustMode();
                BeginIconCounterEdit(vm);
                e.Handled = true;
            }
        }

        private void IconCounterHost_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Border host || DataContext is not ViewModels.MainViewModel vm)
            {
                return;
            }

            var point = e.GetCurrentPoint(host).Properties;
            if (point.IsMiddleButtonPressed)
            {
                if (_isIconCounterMiddleAdjustActive)
                {
                    StopIconCounterMiddleAdjustMode();
                }
                else
                {
                    StartIconCounterMiddleAdjustMode(host, e, vm);
                }

                e.Handled = true;
                return;
            }

            if (_isIconCounterMiddleAdjustActive &&
                (point.IsLeftButtonPressed || point.IsRightButtonPressed))
            {
                StopIconCounterMiddleAdjustMode();
            }
        }

        private void IconCounterHost_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isIconCounterMiddleAdjustActive ||
                sender is not Border host ||
                _iconCounterMiddleScrollViewModel == null)
            {
                return;
            }

            _iconCounterMiddleCurrentY = e.GetPosition(host).Y;
        }

        private void EditView_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isIconCounterMiddleAdjustActive || _iconCounterMiddleScrollViewModel == null)
            {
                return;
            }

            _iconCounterMiddleCurrentY = e.GetPosition(this).Y;
        }

        private void IconCounterHost_PointerExited(object? sender, PointerEventArgs e)
        {
        }

        private void IconCounterHost_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isIconCounterMiddleAdjustActive && e.InitialPressMouseButton != MouseButton.Middle)
            {
                e.Handled = true;
            }
        }

        private void IconCounterDisplay_PointerEntered(object? sender, PointerEventArgs e)
        {
            SetIconCounterScale(1.06);
            var host = this.FindControl<Border>("iconCounterHost");
            if (host != null && ToolTip.GetTip(host) != null)
            {
                ToolTip.SetIsOpen(host, true);
            }
        }

        private void IconCounterDisplay_PointerExited(object? sender, PointerEventArgs e)
        {
            SetIconCounterScale(1.0);
            var host = this.FindControl<Border>("iconCounterHost");
            if (host != null)
            {
                ToolTip.SetIsOpen(host, false);
            }
        }

        private void IconCounterInput_KeyDown(object? sender, KeyEventArgs e)
        {
            if (DataContext is not ViewModels.MainViewModel vm)
            {
                return;
            }

            if (e.Key == Key.Enter)
            {
                CommitIconCounterInput(sender as TextBox, vm);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Up)
            {
                AdjustIconCounter(vm, 1);
                StartIconCounterAutoRepeat(vm, 1, Key.Up);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Down)
            {
                AdjustIconCounter(vm, -1);
                StartIconCounterAutoRepeat(vm, -1, Key.Down);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                StopIconCounterAutoRepeat();
                EndIconCounterEdit();
                e.Handled = true;
            }
        }

        private void IconCounterInput_KeyUp(object? sender, KeyEventArgs e)
        {
            if (_iconCounterRepeatKey == e.Key)
            {
                StopIconCounterAutoRepeat();
                e.Handled = true;
            }
        }

        private void IconCounterInput_LostFocus(object? sender, RoutedEventArgs e)
        {
            StopIconCounterAutoRepeat();
            if (DataContext is ViewModels.MainViewModel vm)
            {
                CommitIconCounterInput(sender as TextBox, vm);
            }
        }

        private void CommitIconCounterInput(TextBox? textBox, ViewModels.MainViewModel vm)
        {
            StopIconCounterAutoRepeat();
            if (textBox == null)
            {
                EndIconCounterEdit();
                return;
            }
            if (!vm.IsIconCounterVisible || !vm.IconCounterDenominator.Any(char.IsDigit))
            {
                SyncIconCounterInputText(vm, preserveSelection: false);
                EndIconCounterEdit();
                return;
            }

            if (int.TryParse(textBox.Text, out var value))
            {
                vm.JumpToIconIndex(value);
            }
            else
            {
                SyncIconCounterInputText(vm, preserveSelection: false);
            }
            EndIconCounterEdit();
        }

        private void AdjustIconCounter(ViewModels.MainViewModel vm, int delta)
        {
            vm.MoveIconIndex(delta, wrap: true);
            SyncIconCounterInputText(vm);
        }

        private void StartIconCounterAutoRepeat(ViewModels.MainViewModel vm, int delta, Key repeatKey)
        {
            if (_iconCounterRepeatKey == repeatKey && _iconCounterRepeatStartTimer.IsEnabled)
            {
                return;
            }

            _iconCounterRepeatDelta = delta;
            _iconCounterRepeatKey = repeatKey;
            _iconCounterRepeatStartTimer.Stop();
            _iconCounterRepeatTickTimer.Stop();
            _iconCounterRepeatViewModel = vm;
            _iconCounterRepeatStartTimer.Start();
        }

        private void StopIconCounterAutoRepeat()
        {
            _iconCounterRepeatStartTimer.Stop();
            _iconCounterRepeatTickTimer.Stop();
            _iconCounterRepeatKey = null;
            _iconCounterRepeatDelta = 0;
            _iconCounterRepeatViewModel = null;
        }

        private void StartIconCounterMiddleAdjustMode(Border host, PointerPressedEventArgs e, ViewModels.MainViewModel vm)
        {
            _isIconCounterMiddleAdjustActive = true;
            _iconCounterMiddleScrollViewModel = vm;
            _iconCounterMiddleScrollRemainder = 0;
            _iconCounterMiddleLastDirection = 0;
            _iconCounterMiddleAnchorY = e.GetPosition(this).Y;
            _iconCounterMiddleCurrentY = _iconCounterMiddleAnchorY;
            _iconCounterCapturedPointer = e.Pointer;
            _iconCounterCapturedPointer?.Capture(this);
            UpdateIconCounterHintVisibility();
            _iconCounterMiddleAutoScrollTimer.Start();
        }

        private void StopIconCounterMiddleAdjustMode()
        {
            _isIconCounterMiddleAdjustActive = false;
            StopIconCounterAutoRepeat();
            _iconCounterMiddleAutoScrollTimer.Stop();
            _iconCounterMiddleScrollViewModel = null;
            _iconCounterMiddleScrollRemainder = 0;
            _iconCounterMiddleLastDirection = 0;
            if (_iconCounterCapturedPointer != null)
            {
                _iconCounterCapturedPointer.Capture(null);
                _iconCounterCapturedPointer = null;
            }
            UpdateIconCounterHintVisibility();
        }

        private void EditView_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!_isIconCounterMiddleAdjustActive || e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed)
            {
                return;
            }

            var host = this.FindControl<Border>("iconCounterHost");
            if (host == null)
            {
                StopIconCounterMiddleAdjustMode();
                return;
            }

            if (e.Source is Visual visual && host.IsVisualAncestorOf(visual))
            {
                return;
            }

            StopIconCounterMiddleAdjustMode();
        }

        private void IconCounterRepeatStartTimer_Tick(object? sender, EventArgs e)
        {
            _iconCounterRepeatStartTimer.Stop();
            if (_iconCounterRepeatViewModel is ViewModels.MainViewModel vm && _iconCounterRepeatDelta != 0)
            {
                _iconCounterRepeatTickTimer.Start();
                AdjustIconCounter(vm, _iconCounterRepeatDelta);
            }
        }

        private void IconCounterRepeatTickTimer_Tick(object? sender, EventArgs e)
        {
            if (_iconCounterRepeatViewModel is ViewModels.MainViewModel vm && _iconCounterRepeatDelta != 0)
            {
                AdjustIconCounter(vm, _iconCounterRepeatDelta);
                return;
            }

            StopIconCounterAutoRepeat();
        }

        private void IconCounterMiddleAutoScrollTimer_Tick(object? sender, EventArgs e)
        {
            if (!_isIconCounterMiddleAdjustActive || _iconCounterMiddleScrollViewModel == null)
            {
                StopIconCounterMiddleAdjustMode();
                return;
            }

            var distance = _iconCounterMiddleCurrentY - _iconCounterMiddleAnchorY;
            var absDistance = Math.Abs(distance);
            var deadZone = 2.0;
            if (absDistance <= deadZone)
            {
                _iconCounterMiddleScrollRemainder = 0;
                _iconCounterMiddleLastDirection = 0;
                return;
            }

            var effectiveDistance = absDistance - deadZone;
            var itemsPerSecond = Math.Min(84.0, 2.0 + Math.Pow(effectiveDistance / 4.8, 1.45));
            var delta = distance < 0 ? 1 : -1;
            if (_iconCounterMiddleLastDirection != 0 && _iconCounterMiddleLastDirection != delta)
            {
                _iconCounterMiddleScrollRemainder = 0;
            }

            _iconCounterMiddleLastDirection = delta;
            var steps = (itemsPerSecond * _iconCounterMiddleAutoScrollTimer.Interval.TotalSeconds) + _iconCounterMiddleScrollRemainder;
            var wholeSteps = (int)Math.Floor(steps);
            _iconCounterMiddleScrollRemainder = steps - wholeSteps;

            if (wholeSteps <= 0)
            {
                return;
            }

            AdjustIconCounter(_iconCounterMiddleScrollViewModel, delta * wholeSteps);
        }

        private void AliasInput_KeyDown(object? sender, KeyEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                if (e.Key == Key.Tab)
                {
                    if (vm.TryAcceptOrExpandAliasAutocomplete())
                    {
                        if (sender is TextBox tabTextBox)
                        {
                            tabTextBox.CaretIndex = vm.Alias?.Length ?? 0;
                        }
                        e.Handled = true;
                    }
                    return;
                }

                if (e.Key == Key.Enter)
                {
                    if (vm.SaveCommand.CanExecute(null))
                    {
                        vm.SaveCommand.Execute(null);
                        e.Handled = true;
                    }
                    return;
                }

                if (e.Key == Key.Up)
                {
                    vm.NavigateSavedAliasHistory(-1);
                    if (sender is TextBox upTextBox)
                    {
                        upTextBox.CaretIndex = vm.Alias?.Length ?? 0;
                    }
                    e.Handled = true;
                    return;
                }

                if (e.Key == Key.Down)
                {
                    vm.NavigateSavedAliasHistory(1);
                    if (sender is TextBox downTextBox)
                    {
                        downTextBox.CaretIndex = vm.Alias?.Length ?? 0;
                    }
                    e.Handled = true;
                    return;
                }

                if (e.Key is Key.Back or Key.Space)
                {
                    vm.DismissAliasAutocomplete();
                }
            }
        }

        private void AliasInput_TextChanged(object? sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (textBox.Text?.Length >= 260)
                {
                    // Set to red color for warning (#FFFF5555)
                    textBox.Foreground = new SolidColorBrush(Color.Parse("#FFFF5555"));
                }
                else
                {
                    // Restore default color - use the default foreground from resources
                    if (textBox.TryFindResource("Fg1Brush", out var brush) && brush is SolidColorBrush colorBrush)
                    {
                        textBox.Foreground = colorBrush;
                    }
                    else
                    {
                        textBox.Foreground = Brushes.White;
                    }
                }
            }
        }

        private void AliasAutocompleteScrollViewer_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm && vm.IsAliasAutocompleteExpanded)
            {
                vm.CycleAliasAutocompleteSelection(e.Delta.Y > 0 ? -1 : 1);
                var aliasInput = this.FindControl<TextBox>("aliasInput");
                if (aliasInput != null)
                {
                    aliasInput.CaretIndex = vm.Alias?.Length ?? 0;
                }
                e.Handled = true;
            }
        }

        private void IconListScrollViewer_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (_iconListScrollViewer == null)
            {
                return;
            }

            if (_iconListScrollViewer.Extent.Height <= _iconListScrollViewer.Viewport.Height)
            {
                e.Handled = true;
                return;
            }

            var currentOffset = _iconListScrollViewer.Offset;
            var maxOffset = Math.Max(0, _iconListScrollViewer.Extent.Height - _iconListScrollViewer.Viewport.Height);
            var step = Math.Abs(e.Delta.Y) switch
            {
                < 0.01 => 36,
                < 1.5 => 48,
                _ => 72
            };
            var direction = e.Delta.Y > 0 ? -1 : 1;
            var nextY = Math.Clamp(currentOffset.Y + (direction * step), 0, maxOffset);

            if (Math.Abs(nextY - currentOffset.Y) > 0.01)
            {
                _iconListScrollViewer.Offset = new Vector(currentOffset.X, nextY);
            }

            e.Handled = true;
        }

        private void AliasAutocompleteCandidate_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Button button &&
                button.DataContext is ViewModels.MainViewModel.AliasAutocompleteItemViewModel candidate &&
                DataContext is ViewModels.MainViewModel vm)
            {
                _suppressAliasAutocompleteDismissOnce = true;
                vm.SelectAliasAutocompleteCandidate(candidate);
                var aliasInput = this.FindControl<TextBox>("aliasInput");
                if (aliasInput != null)
                {
                    aliasInput.Focus();
                    aliasInput.CaretIndex = vm.Alias?.Length ?? 0;
                }
                e.Handled = true;
            }
        }

        private void AliasInput_DragOver(object? sender, DragEventArgs e)
        {
            if (e.Data.Contains(DataFormats.Text))
            {
                e.DragEffects = DragDropEffects.Copy;
                e.Handled = true;
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
                e.Handled = true;
            }
        }

        private void AliasInput_Drop(object? sender, DragEventArgs e)
        {
            if (sender is TextBox textBox && e.Data.Contains(DataFormats.Text))
            {
                string textData = e.Data.GetText()?.Trim() ?? "";
                if (!string.IsNullOrEmpty(textData))
                {
                    // Clean the text by removing quotes
                    string cleanedText = textData.Trim('"', '\'');

                    if (DataContext is ViewModels.MainViewModel vm)
                    {
                        // Set the alias directly - the property setter will handle undo recording
                        vm.Alias = cleanedText;
                        textBox.Focus();
                        textBox.SelectAll();
                    }
                }

                e.Handled = true;
            }
        }
 
        private void TitleBarButtons_BackRequested(object? sender, RoutedEventArgs e)
        {
            if (this.VisualRoot is MainWindow mainWindow)
            {
                mainWindow.GoToHomeView();
            }
        }

        private async void EditTitleButton_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                await vm.ToggleElevationSessionAsync();
            }
        }

        private void LocalizationManager_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == string.Empty)
            {
                Dispatcher.UIThread.Post(() => ReloadEditDragTipItems(resetIndex: false), DispatcherPriority.Background);
            }
        }

        private void EnsureDragTipTransforms()
        {
            if (_editDragTipLeadText != null)
            {
                _editDragTipLeadBlur ??= new BlurEffect { Radius = 0 };
                _editDragTipLeadText.Effect = _editDragTipLeadBlur;
            }

            if (_editDragTipTrailText != null)
            {
                _editDragTipTrailBlur ??= new BlurEffect { Radius = 0 };
                _editDragTipTrailText.Effect = _editDragTipTrailBlur;
            }

            if (_editDragTipCurrentText != null)
            {
                _editDragTipCurrentScale ??= new ScaleTransform(1, 1);
                _editDragTipCurrentSkew ??= new SkewTransform(0, 0);
                _editDragTipCurrentTranslate ??= new TranslateTransform(0, 0);
                _editDragTipCurrentBlur ??= new BlurEffect { Radius = 0 };
                _editDragTipCurrentText.RenderTransform = new TransformGroup
                {
                    Children = new Transforms
                    {
                        _editDragTipCurrentScale,
                        _editDragTipCurrentSkew,
                        _editDragTipCurrentTranslate
                    }
                };
                _editDragTipCurrentText.Effect = _editDragTipCurrentBlur;
            }

            if (_editDragTipCurrentGhostText != null)
            {
                _editDragTipCurrentGhostTranslate ??= new TranslateTransform(0, 0);
                _editDragTipCurrentGhostBlur ??= new BlurEffect { Radius = 0 };
                _editDragTipCurrentGhostText.RenderTransform = _editDragTipCurrentGhostTranslate;
                _editDragTipCurrentGhostText.Effect = _editDragTipCurrentGhostBlur;
            }

            if (_editDragTipNextText != null)
            {
                _editDragTipNextScale ??= new ScaleTransform(1, 1);
                _editDragTipNextSkew ??= new SkewTransform(0, 0);
                _editDragTipNextTranslate ??= new TranslateTransform(0, 0);
                _editDragTipNextBlur ??= new BlurEffect { Radius = 0 };
                _editDragTipNextText.RenderTransform = new TransformGroup
                {
                    Children = new Transforms
                    {
                        _editDragTipNextScale,
                        _editDragTipNextSkew,
                        _editDragTipNextTranslate
                    }
                };
                _editDragTipNextText.Effect = _editDragTipNextBlur;
            }

            if (_editDragTipNextGhostText != null)
            {
                _editDragTipNextGhostTranslate ??= new TranslateTransform(0, 0);
                _editDragTipNextGhostBlur ??= new BlurEffect { Radius = 0 };
                _editDragTipNextGhostText.RenderTransform = _editDragTipNextGhostTranslate;
                _editDragTipNextGhostText.Effect = _editDragTipNextGhostBlur;
            }
        }

        private void ReloadEditDragTipItems(bool resetIndex)
        {
            _editDragTipItems.Clear();
            var config = ConfigManager.Config.EditHintCarousel ?? new EditHintCarouselConfig();
            _editDragTipRotationIntervalSeconds = Math.Max(1.5, config.RotationIntervalSeconds);
            var orderedKeys = config.EnabledItems is { Length: > 0 }
                ? config.EnabledItems
                : new[] { "folder", "icon", "image", "alias" };

            foreach (var key in orderedKeys)
            {
                var item = BuildEditDragTipItem(key, config);
                if (item != null)
                {
                    _editDragTipItems.Add(item);
                }
            }

            if (_editDragTipItems.Count == 0)
            {
                _editDragTipItems.Add(BuildEditDragTipItem("folder", config)!);
            }

            if (resetIndex || _editDragTipCurrentIndex >= _editDragTipItems.Count)
            {
                _editDragTipCurrentIndex = 0;
            }

            _editDragTipNextIndex = -1;
            _isDragTipAnimating = false;
            _editDragTipAnimationProgress = 0;
            _editDragTipElapsedSeconds = 0;
            _lastDragTipAmbientTickSeconds = 0;
            ApplyDragTipSnapshot();
            UpdateDragTipAmbientState();
        }

        private EditDragTipItem? BuildEditDragTipItem(string key, EditHintCarouselConfig config)
        {
            var normalizedKey = (key ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                return null;
            }

            var loc = LocalizationManager.Instance;
            return normalizedKey switch
            {
                "folder" => new EditDragTipItem("folder", loc["Edit_DragTip_Item_Folder"], ParseConfiguredColor(config.FolderGradientStart), ParseConfiguredColor(config.FolderGradientEnd)),
                "icon" => new EditDragTipItem("icon", loc["Edit_DragTip_Item_Icon"], ParseConfiguredColor(config.IconGradientStart), ParseConfiguredColor(config.IconGradientEnd)),
                "image" => new EditDragTipItem("image", loc["Edit_DragTip_Item_Image"], ParseConfiguredColor(config.ImageGradientStart), ParseConfiguredColor(config.ImageGradientEnd)),
                "alias" => new EditDragTipItem("alias", loc["Edit_DragTip_Item_Alias"], ParseConfiguredColor(config.AliasGradientStart), ParseConfiguredColor(config.AliasGradientEnd)),
                _ => null
            };
        }

        private static Color ParseConfiguredColor(string value)
        {
            try
            {
                return Color.Parse(value);
            }
            catch
            {
                return Color.Parse("#FDA085");
            }
        }

        private void OnDragTipAmbientTick(double nowSeconds)
        {
            if (_editDragTipCurrentText == null || _editDragTipItems.Count == 0 || !IsVisible || this.GetVisualRoot() == null)
            {
                return;
            }

            if (_lastDragTipAmbientTickSeconds <= 0)
            {
                _lastDragTipAmbientTickSeconds = nowSeconds;
                return;
            }

            var deltaSeconds = Math.Clamp(nowSeconds - _lastDragTipAmbientTickSeconds, 0, 0.25);
            _lastDragTipAmbientTickSeconds = nowSeconds;

            if (_editDragTipItems.Count <= 1)
            {
                return;
            }

            if (_isDragTipAnimating)
            {
                _editDragTipAnimationProgress = Math.Min(1.0, _editDragTipAnimationProgress + (deltaSeconds / DragTipAnimationDurationSeconds));
                ApplyDragTipSnapshot();
                if (_editDragTipAnimationProgress >= 1.0)
                {
                    CompleteDragTipAnimation();
                }
                return;
            }

            _editDragTipElapsedSeconds += deltaSeconds;
            if (_editDragTipElapsedSeconds >= _editDragTipRotationIntervalSeconds)
            {
                StartDragTipAnimation();
            }
        }

        private void StartDragTipAnimation()
        {
            if (_editDragTipItems.Count <= 1)
            {
                return;
            }

            _editDragTipElapsedSeconds = 0;
            _editDragTipAnimationProgress = 0;
            _isDragTipAnimating = true;
            _editDragTipNextIndex = (_editDragTipCurrentIndex + 1) % _editDragTipItems.Count;
            ApplyDragTipSnapshot();
        }

        private void CompleteDragTipAnimation()
        {
            if (_editDragTipNextIndex >= 0)
            {
                _editDragTipCurrentIndex = _editDragTipNextIndex;
            }

            _editDragTipNextIndex = -1;
            _isDragTipAnimating = false;
            _editDragTipAnimationProgress = 0;
            ApplyDragTipSnapshot();
        }

        private void ApplyDragTipSnapshot()
        {
            EnsureDragTipTransforms();
            if (_editDragTipCurrentText == null || _editDragTipItems.Count == 0)
            {
                return;
            }

            var currentItem = _editDragTipItems[Math.Clamp(_editDragTipCurrentIndex, 0, _editDragTipItems.Count - 1)];
            _editDragTipCurrentText.Text = currentItem.Text;
            ApplyDragTipPalette(_editDragTipCurrentBrush, currentItem.StartColor, currentItem.EndColor);
            if (_editDragTipCurrentGhostText != null)
            {
                _editDragTipCurrentGhostText.Text = currentItem.Text;
            }

            if (_editDragTipNextText == null || !_isDragTipAnimating || _editDragTipNextIndex < 0 || _editDragTipNextIndex >= _editDragTipItems.Count)
            {
                if (_editDragTipCurrentTranslate != null)
                {
                    _editDragTipCurrentTranslate.Y = 0;
                }
                if (_editDragTipCurrentScale != null)
                {
                    _editDragTipCurrentScale.ScaleY = 1;
                }
                if (_editDragTipCurrentSkew != null)
                {
                    _editDragTipCurrentSkew.AngleX = 0;
                }
                if (_editDragTipCurrentBlur != null)
                {
                    _editDragTipCurrentBlur.Radius = 0;
                }
                if (_editDragTipCurrentGhostTranslate != null)
                {
                    _editDragTipCurrentGhostTranslate.Y = 0;
                }
                if (_editDragTipCurrentGhostBlur != null)
                {
                    _editDragTipCurrentGhostBlur.Radius = 0;
                }
                if (_editDragTipLeadBlur != null)
                {
                    _editDragTipLeadBlur.Radius = 0;
                }
                if (_editDragTipTrailBlur != null)
                {
                    _editDragTipTrailBlur.Radius = 0;
                }
                if (_editDragTipLeadText != null)
                {
                    _editDragTipLeadText.Opacity = 1;
                }
                if (_editDragTipTrailText != null)
                {
                    _editDragTipTrailText.Opacity = 1;
                }
                _editDragTipCurrentText.Opacity = 1;
                if (_editDragTipCurrentGhostText != null)
                {
                    _editDragTipCurrentGhostText.IsVisible = false;
                    _editDragTipCurrentGhostText.Opacity = 0;
                }

                if (_editDragTipNextText != null)
                {
                    _editDragTipNextText.IsVisible = false;
                    _editDragTipNextText.Opacity = 0;
                    if (_editDragTipNextBlur != null)
                    {
                        _editDragTipNextBlur.Radius = 0;
                    }
                }
                if (_editDragTipNextGhostText != null)
                {
                    _editDragTipNextGhostText.IsVisible = false;
                    _editDragTipNextGhostText.Opacity = 0;
                }
                if (_editDragTipNextGhostTranslate != null)
                {
                    _editDragTipNextGhostTranslate.Y = 0;
                }
                if (_editDragTipNextGhostBlur != null)
                {
                    _editDragTipNextGhostBlur.Radius = 0;
                }

                UpdateDragTipSlotWidth(currentItem.Text);
                return;
            }

            var nextItem = _editDragTipItems[_editDragTipNextIndex];
            _editDragTipNextText.Text = nextItem.Text;
            _editDragTipNextText.IsVisible = true;
            ApplyDragTipPalette(_editDragTipNextBrush, nextItem.StartColor, nextItem.EndColor);
            if (_editDragTipNextGhostText != null)
            {
                _editDragTipNextGhostText.Text = nextItem.Text;
                _editDragTipNextGhostText.IsVisible = true;
            }
            if (_editDragTipCurrentGhostText != null)
            {
                _editDragTipCurrentGhostText.IsVisible = true;
            }

            var eased = EaseOutCubic(_editDragTipAnimationProgress);
            var motionEnvelope = ComputeDragTipMotionEnvelope(_editDragTipAnimationProgress);
            var trailingDistance = 4.0 + (10.0 * motionEnvelope);
            if (_editDragTipCurrentTranslate != null)
            {
                _editDragTipCurrentTranslate.Y = -DragTipTravelDistance * eased;
            }
            if (_editDragTipCurrentScale != null)
            {
                _editDragTipCurrentScale.ScaleY = 1.0 - (0.16 * eased);
            }
            if (_editDragTipCurrentSkew != null)
            {
                _editDragTipCurrentSkew.AngleX = 8.0 * eased;
            }
            var blurEnvelope = ComputeDragTipBlurEnvelope(_editDragTipAnimationProgress);
            if (_editDragTipLeadBlur != null)
            {
                _editDragTipLeadBlur.Radius = 1.4 * blurEnvelope;
            }
            if (_editDragTipTrailBlur != null)
            {
                _editDragTipTrailBlur.Radius = 1.4 * blurEnvelope;
            }
            if (_editDragTipLeadText != null)
            {
                _editDragTipLeadText.Opacity = 0.92 + (0.08 * (1.0 - blurEnvelope));
            }
            if (_editDragTipTrailText != null)
            {
                _editDragTipTrailText.Opacity = 0.92 + (0.08 * (1.0 - blurEnvelope));
            }
            if (_editDragTipCurrentBlur != null)
            {
                _editDragTipCurrentBlur.Radius = 2.4 * blurEnvelope;
            }
            _editDragTipCurrentText.Opacity = 1.0 - (0.48 * eased);
            if (_editDragTipCurrentGhostTranslate != null)
            {
                _editDragTipCurrentGhostTranslate.Y = (-DragTipTravelDistance * eased) + trailingDistance;
            }
            if (_editDragTipCurrentGhostBlur != null)
            {
                _editDragTipCurrentGhostBlur.Radius = 1.6 + (3.2 * motionEnvelope);
            }
            if (_editDragTipCurrentGhostText != null)
            {
                _editDragTipCurrentGhostText.Opacity = 0.18 * motionEnvelope * (1.0 - eased);
            }

            if (_editDragTipNextTranslate != null)
            {
                _editDragTipNextTranslate.Y = DragTipTravelDistance * (1.0 - eased);
            }
            if (_editDragTipNextScale != null)
            {
                _editDragTipNextScale.ScaleY = 0.84 + (0.16 * eased);
            }
            if (_editDragTipNextSkew != null)
            {
                _editDragTipNextSkew.AngleX = -8.0 * (1.0 - eased);
            }
            if (_editDragTipNextBlur != null)
            {
                _editDragTipNextBlur.Radius = 3.0 * blurEnvelope;
            }
            _editDragTipNextText.Opacity = 0.42 + (0.58 * eased);
            if (_editDragTipNextGhostTranslate != null)
            {
                _editDragTipNextGhostTranslate.Y = (DragTipTravelDistance * (1.0 - eased)) - trailingDistance;
            }
            if (_editDragTipNextGhostBlur != null)
            {
                _editDragTipNextGhostBlur.Radius = 1.8 + (3.4 * motionEnvelope);
            }
            if (_editDragTipNextGhostText != null)
            {
                _editDragTipNextGhostText.Opacity = 0.2 * motionEnvelope * eased;
            }
            UpdateDragTipSlotWidth(currentItem.Text, nextItem.Text);
        }

        private void UpdateDragTipSlotWidth(string primaryText, string? secondaryText = null)
        {
            if (_editDragTipSlot == null)
            {
                return;
            }

            var primaryWidth = MeasureDragTipTextWidth(primaryText);
            var secondaryWidth = string.IsNullOrWhiteSpace(secondaryText)
                ? primaryWidth
                : MeasureDragTipTextWidth(secondaryText);

            double targetWidth;
            if (_isDragTipAnimating && !string.IsNullOrWhiteSpace(secondaryText))
            {
                // Let the slot width start reacting early instead of waiting until the old long label fully leaves.
                var widthProgress = EaseOutCubic(Math.Clamp(_editDragTipAnimationProgress * 1.35, 0, 1));
                targetWidth = primaryWidth + ((secondaryWidth - primaryWidth) * widthProgress);
            }
            else
            {
                targetWidth = primaryWidth;
            }

            _editDragTipSlot.Width = Math.Max(22, targetWidth + 2);
        }

        private static double ComputeDragTipBlurEnvelope(double progress)
        {
            var t = Math.Clamp(progress, 0, 1);
            if (t <= 0.18)
            {
                return EaseOutCubic(t / 0.18);
            }

            if (t <= 0.34)
            {
                return 1.0;
            }

            if (t <= 0.68)
            {
                var fade = (t - 0.34) / 0.34;
                return 1.0 - EaseOutCubic(fade);
            }

            return 0;
        }

        private static double ComputeDragTipMotionEnvelope(double progress)
        {
            var t = Math.Clamp(progress, 0, 1);
            if (t <= 0.08)
            {
                return EaseOutCubic(t / 0.08);
            }

            if (t <= 0.58)
            {
                return 1.0;
            }

            if (t <= 0.9)
            {
                return 1.0 - EaseOutCubic((t - 0.58) / 0.32);
            }

            return 0;
        }

        private double MeasureDragTipTextWidth(string? text)
        {
            if (string.IsNullOrWhiteSpace(text) || _editDragTipCurrentText == null)
            {
                return 44;
            }

            _editDragTipCurrentText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var typeface = new Typeface(_editDragTipCurrentText.FontFamily, _editDragTipCurrentText.FontStyle, _editDragTipCurrentText.FontWeight);
            var formatted = new FormattedText(
                text,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface,
                _editDragTipCurrentText.FontSize,
                Brushes.White);
            return formatted.WidthIncludingTrailingWhitespace;
        }

        private void ApplyDragTipPalette(LinearGradientBrush? brush, Color startColor, Color endColor)
        {
            if (brush == null)
            {
                return;
            }

            var midColor = BlendColors(startColor, endColor, 0.5);
            if (brush.GradientStops.Count != 4)
            {
                brush.GradientStops.Clear();
                brush.GradientStops.Add(new GradientStop(startColor, 0));
                brush.GradientStops.Add(new GradientStop(midColor, 0.38));
                brush.GradientStops.Add(new GradientStop(endColor, 0.82));
                brush.GradientStops.Add(new GradientStop(endColor, 1));
                return;
            }

            brush.GradientStops[0].Color = startColor;
            brush.GradientStops[1].Color = midColor;
            brush.GradientStops[2].Color = endColor;
            brush.GradientStops[3].Color = endColor;
        }

        private static Color BlendColors(Color startColor, Color endColor, double ratio)
        {
            var t = Math.Clamp(ratio, 0, 1);
            return Color.FromArgb(
                (byte)(startColor.A + ((endColor.A - startColor.A) * t)),
                (byte)(startColor.R + ((endColor.R - startColor.R) * t)),
                (byte)(startColor.G + ((endColor.G - startColor.G) * t)),
                (byte)(startColor.B + ((endColor.B - startColor.B) * t)));
        }

        private static double EaseOutCubic(double value)
        {
            var t = Math.Clamp(value, 0, 1);
            var inv = 1 - t;
            return 1 - (inv * inv * inv);
        }

        private void ElevationSessionState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ElevationSessionState.IsElevatedSessionActive) && _elevationSessionState != null)
            {
                ApplyEditTitleStyle(_elevationSessionState.IsElevatedSessionActive);
            }
        }

        private void ApplyEditTitleStyle(bool isElevated)
        {
            if (_editTitleText == null)
            {
                return;
            }

            if (isElevated && _titleGoldBrush != null)
            {
                EnsureTitleGradientPattern();
                UpdateTitleGradientOffsets();
                _editTitleText.Foreground = _titleGoldBrush;
                _isAdminTitleAmbientEnabled = true;
                UpdateAdminAmbientState();
                return;
            }

            _isAdminTitleAmbientEnabled = false;
            UpdateAdminAmbientState();
            _editTitleText.Foreground = Avalonia.Application.Current?.TryGetResource("Fg2Brush", null, out var brush) == true && brush is IBrush fgBrush
                ? fgBrush
                : new SolidColorBrush(Color.Parse("#606064"));
        }

        private void OnAdminTitleAmbientTick(double nowSeconds)
        {
            if (_titleGoldBrush == null || !_isAdminTitleAmbientEnabled || !IsVisible || this.GetVisualRoot() == null)
            {
                return;
            }

            if (_lastTitleAmbientTickSeconds <= 0)
            {
                _lastTitleAmbientTickSeconds = nowSeconds;
                return;
            }

            var deltaSeconds = Math.Clamp(nowSeconds - _lastTitleAmbientTickSeconds, 0, 0.2);
            _lastTitleAmbientTickSeconds = nowSeconds;
            _titleGradientPhase += deltaSeconds / 6.0;
            if (_titleGradientPhase >= 1)
            {
                _titleGradientPhase -= 1;
            }

            EnsureTitleGradientPattern();
            UpdateTitleGradientOffsets();
        }

        private void EnsureTitleGradientPattern()
        {
            if (_titleGoldBrush == null)
            {
                return;
            }

            var expectedStopCount = (_titleGradientColors.Length * TitleGradientCycles) + 1;
            if (_titleGoldBrush.GradientStops.Count == expectedStopCount)
            {
                return;
            }

            _titleGoldBrush.GradientStops.Clear();
            for (var cycle = 0; cycle < TitleGradientCycles; cycle++)
            {
                for (var colorIndex = 0; colorIndex < _titleGradientColors.Length; colorIndex++)
                {
                    _titleGoldBrush.GradientStops.Add(new GradientStop(
                        _titleGradientColors[colorIndex],
                        cycle + (colorIndex / (double)_titleGradientColors.Length)));
                }
            }

            _titleGoldBrush.GradientStops.Add(new GradientStop(_titleGradientColors[0], TitleGradientCycles));
        }

        private void UpdateTitleGradientOffsets()
        {
            if (_titleGoldBrush == null)
            {
                return;
            }

            var stopIndex = 0;
            for (var cycle = 0; cycle < TitleGradientCycles; cycle++)
            {
                for (var colorIndex = 0; colorIndex < _titleGradientColors.Length; colorIndex++)
                {
                    _titleGoldBrush.GradientStops[stopIndex].Color = _titleGradientColors[colorIndex];
                    _titleGoldBrush.GradientStops[stopIndex].Offset = cycle + (colorIndex / (double)_titleGradientColors.Length) - _titleGradientPhase;
                    stopIndex++;
                }
            }

            _titleGoldBrush.GradientStops[stopIndex].Color = _titleGradientColors[0];
            _titleGoldBrush.GradientStops[stopIndex].Offset = TitleGradientCycles - _titleGradientPhase;
        }

        private void UpdateAdminAmbientState()
        {
            if (_adminTitleAmbientHandle == null)
            {
                return;
            }

            var enabled = !_isAmbientSuspended && !DebugRuntimeAnalysis.PauseAnimations && _isAdminTitleAmbientEnabled && IsVisible && VisualRoot != null;
            if (!enabled)
            {
                _lastTitleAmbientTickSeconds = 0;
            }

            _adminTitleAmbientHandle.SetEnabled(enabled);
        }

        private void UpdateDragTipAmbientState()
        {
            if (_dragTipAmbientHandle == null)
            {
                return;
            }

            var enabled = !_isAmbientSuspended && !DebugRuntimeAnalysis.PauseAnimations && IsVisible && VisualRoot != null && _editDragTipItems.Count > 1;
            if (!enabled)
            {
                _lastDragTipAmbientTickSeconds = 0;
            }

            _dragTipAmbientHandle.SetEnabled(enabled);
        }

        private void DebugRuntimeAnalysis_PauseAnimationsChanged(object? sender, EventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                UpdateAdminAmbientState();
                UpdateDragTipAmbientState();
            }, DispatcherPriority.Background);
        }

        private async void BtnPickDir_Click(object? sender, RoutedEventArgs e)
        {
            if (this.VisualRoot is Window window)
            {
                var topLevel = TopLevel.GetTopLevel(window);
                if (topLevel != null)
                {
                    var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                    {
                        Title = Services.LocalizationManager.Instance["Home_FolderPicker_Title"],
                        AllowMultiple = false
                    });

                    if (folders.Count > 0)
                    {
                        var pathInput = this.FindControl<TextBox>("pathInput");
                        if (pathInput != null)
                        {
                            pathInput.Text = folders[0].Path.LocalPath;
                        }
                    }
                }
            }
        }

        private async void BtnPickIcon_Click(object? sender, RoutedEventArgs e)
        {
            if (this.VisualRoot is Window window)
            {
                var topLevel = TopLevel.GetTopLevel(window);
                if (topLevel != null)
                {
                    var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = Services.LocalizationManager.Instance["Edit_Icon_Select"],
                        AllowMultiple = false,
                        FileTypeFilter = new[]
                        {
                            new FilePickerFileType(Services.LocalizationManager.Instance["Edit_Icon_Select"])
                            {
                                Patterns = new[] { "*.ico", "*.exe", "*.dll", "*.png", "*.jpg", "*.jpeg", "*.svg", "*.gif", "*.bmp" }
                            }
                        }
                    });

                    if (files.Count > 0)
                    {
                        var iconInput = this.FindControl<TextBox>("iconInput");
                        if (iconInput != null)
                        {
                            iconInput.Text = files[0].Path.LocalPath;
                        }
                    }
                }
            }
        }

        private void BtnOpenExplorer_Click(object? sender, RoutedEventArgs e)
        {
            var pathInput = this.FindControl<TextBox>("pathInput");
            if (pathInput != null && !string.IsNullOrEmpty(pathInput.Text))
            {
                try
                {
                    System.Diagnostics.Process.Start("explorer.exe", pathInput.Text);
                }
                catch (Exception ex)
                {
                    // Handle error - could show a toast or dialog
                    Console.WriteLine($"Failed to open explorer: {ex.Message}");
                }
            }
        }

        private void IconListBox_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is ListBox listBox)
            {
                Dispatcher.UIThread.Post(() => listBox.Focus(), DispatcherPriority.Input);
            }
        }

        private void IconListBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (sender is not ListBox || DataContext is not ViewModels.MainViewModel vm || vm.Icons.Count == 0)
            {
                return;
            }

            var currentAnchor = vm.PreviewedIcon ?? vm.SelectedIcon;
            var currentIndex = currentAnchor != null ? vm.Icons.IndexOf(currentAnchor) : 0;
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            var nextIndex = currentIndex;

            switch (e.Key)
            {
                case Key.Left:
                case Key.A:
                    nextIndex = Math.Max(0, currentIndex - 1);
                    break;
                case Key.Right:
                case Key.D:
                    nextIndex = Math.Min(vm.Icons.Count - 1, currentIndex + 1);
                    break;
                case Key.Up:
                case Key.W:
                    nextIndex = currentIndex < ViewModels.MainViewModel.IconGridColumns
                        ? currentIndex
                        : currentIndex - ViewModels.MainViewModel.IconGridColumns;
                    break;
                case Key.Down:
                case Key.S:
                    nextIndex = Math.Min(vm.Icons.Count - 1, currentIndex + ViewModels.MainViewModel.IconGridColumns);
                    break;
                case Key.Enter:
                    if (vm.PreviewedIcon != null && ReferenceEquals(vm.PreviewedIcon, vm.SelectedIcon))
                    {
                        ExecuteSaveCommand(vm);
                    }
                    else if (vm.PreviewedIcon != null)
                    {
                        vm.SelectedIcon = vm.PreviewedIcon;
                    }
                    e.Handled = true;
                    return;
                default:
                    return;
            }

            e.Handled = true;

            if (nextIndex != currentIndex && nextIndex >= 0 && nextIndex < vm.Icons.Count)
            {
                vm.PreviewedIcon = vm.Icons[nextIndex];
                SmoothScrollIconPreviewIntoView();
            }
        }

        private void SubscribeToViewModel()
        {
            if (DataContext is INotifyPropertyChanged notifyPropertyChanged)
            {
                notifyPropertyChanged.PropertyChanged -= ViewModel_PropertyChanged;
                notifyPropertyChanged.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        private void UnsubscribeFromViewModel()
        {
            if (DataContext is INotifyPropertyChanged notifyPropertyChanged)
            {
                notifyPropertyChanged.PropertyChanged -= ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not ViewModels.MainViewModel vm)
            {
                return;
            }

            if (!_pendingInitialScrollReset ||
                e.PropertyName != nameof(ViewModels.MainViewModel.IsLoadingIcons))
            {
                return;
            }

            if (!vm.IsLoadingIcons)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    ResetEditScrollToTop();
                    _pendingInitialScrollReset = false;
                }, DispatcherPriority.Background);
            }
        }

        private void ResetEditScrollToTop()
        {
            var editScrollViewer = this.FindControl<ScrollViewer>("editScrollViewer");
            if (editScrollViewer == null)
            {
                return;
            }

            editScrollViewer.Offset = new Vector(editScrollViewer.Offset.X, 0);
        }

        private double GetIconRowHeight()
        {
            var iconCellFrame = this
                .GetVisualDescendants()
                .OfType<Border>()
                .FirstOrDefault(border => border.Classes.Contains("IconCellFrame"));

            if (iconCellFrame != null)
            {
                return iconCellFrame.Bounds.Height + iconCellFrame.Margin.Top + iconCellFrame.Margin.Bottom;
            }

            return 70;
        }

        private static void ExecuteSaveCommand(ViewModels.MainViewModel vm)
        {
            if (vm.SaveCommand.CanExecute(null))
            {
                vm.SaveCommand.Execute(null);
            }
        }

        private void IconCellBorder_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed ||
                sender is not Border { DataContext: ViewModels.IconViewModel icon } ||
                DataContext is not ViewModels.MainViewModel vm)
            {
                return;
            }

            var preservedEditOffset = _editScrollViewer?.Offset.Y ?? 0;
            vm.SelectedIcon = icon;

            var iconRowListBox = this.FindControl<ListBox>("iconRowListBox");
            Dispatcher.UIThread.Post(() =>
            {
                iconRowListBox?.Focus();
                if (_editScrollViewer != null)
                {
                    _editScrollViewer.Offset = new Vector(_editScrollViewer.Offset.X, preservedEditOffset);
                }
            }, DispatcherPriority.Input);

            if (e.ClickCount >= 2)
            {
                ExecuteSaveCommand(vm);
            }

            e.Handled = true;
        }

        private void SmoothScrollIconPreviewIntoView()
        {
            if (_iconListScrollViewer == null || DataContext is not ViewModels.MainViewModel vm)
            {
                return;
            }

            var previewedIcon = vm.PreviewedIcon ?? vm.SelectedIcon;
            if (previewedIcon == null)
            {
                return;
            }

            var previewIndex = vm.Icons.IndexOf(previewedIcon);
            if (previewIndex < 0)
            {
                return;
            }

            var currentOffset = _iconListScrollViewer.Offset;
            var rowHeight = GetIconRowHeight();
            var rowIndex = previewIndex / ViewModels.MainViewModel.IconGridColumns;
            var top = (rowIndex * rowHeight) - currentOffset.Y;
            var bottom = top + rowHeight;
            var viewportHeight = _iconListScrollViewer.Viewport.Height;

            if (top < 0)
            {
                _iconListTargetOffsetY = Math.Max(0, currentOffset.Y + top - 8);
            }
            else if (bottom > viewportHeight)
            {
                var maxOffset = Math.Max(0, _iconListScrollViewer.Extent.Height - viewportHeight);
                _iconListTargetOffsetY = Math.Min(maxOffset, currentOffset.Y + (bottom - viewportHeight) + 8);
            }
            else
            {
                return;
            }

            _iconListScrollAnimator.AnimateTo(_iconListTargetOffsetY, MotionProfile.Smooth(TimeSpan.FromMilliseconds(170)));
        }

        private void EditScrollViewer_RequestBringIntoView(object? sender, RequestBringIntoViewEventArgs e)
        {
            if (sender is not ScrollViewer editScrollViewer)
            {
                return;
            }

            if (e.TargetObject is not Visual target)
            {
                return;
            }

            if (target.FindAncestorOfType<ListBox>() is ListBox listBox && listBox.Name == "iconRowListBox")
            {
                // 阻止图标列表焦点变更把外层编辑页滚动到最底部。
                var current = editScrollViewer.Offset;
                editScrollViewer.Offset = new Vector(current.X, current.Y);
                e.Handled = true;
                return;
            }

            if (target.FindAncestorOfType<TextBox>() is TextBox textBox &&
                textBox.Name == "aliasInput" &&
                !_pendingInitialScrollReset)
            {
                // 窗口重新获得焦点时，Avalonia 会尝试把上次聚焦的输入框重新 BringIntoView。
                // 外层 ScrollViewer 接到这个请求后会重新排版并把视图推到偏下位置。
                e.Handled = true;
            }
        }

        public double GetEditScrollOffsetY()
        {
            return _editScrollViewer?.Offset.Y ?? 0;
        }

        public void RestoreEditScrollOffsetY(double offsetY)
        {
            if (_editScrollViewer == null)
            {
                return;
            }

            _editScrollViewer.Offset = new Vector(_editScrollViewer.Offset.X, Math.Max(0, offsetY));
        }

        private void UpdateIconListScrollAnimatorFrameRate()
        {
            var configured = Math.Clamp(_frameRateSettings?.ActiveInteractionFps ?? 60, 1, 240);
            var displayHz = Math.Clamp(_frameRateSettings?.DisplayRefreshRateHz ?? configured, 1, 500);
            _iconListScrollAnimator.SetFrameRate(Math.Min(configured, displayHz));
        }

        private void BeginIconCounterEdit(ViewModels.MainViewModel vm)
        {
            var display = this.FindControl<TextBlock>("iconCounterDisplay");
            var input = this.FindControl<TextBox>("iconCounterInput");
            if (display == null || input == null) return;

            display.IsVisible = false;
            input.IsVisible = true;
            SyncIconCounterInputText(vm, preserveSelection: false);
            input.Focus();
            input.SelectAll();
            UpdateIconCounterHintVisibility();
        }

        private void EndIconCounterEdit()
        {
            var display = this.FindControl<TextBlock>("iconCounterDisplay");
            var input = this.FindControl<TextBox>("iconCounterInput");
            if (display == null || input == null) return;

            input.IsVisible = false;
            display.IsVisible = true;
            SetIconCounterScale(1.0);
            UpdateIconCounterHintVisibility();
        }

        private void SyncIconCounterInputText(ViewModels.MainViewModel vm, bool preserveSelection = true)
        {
            var input = this.FindControl<TextBox>("iconCounterInput");
            if (input == null)
            {
                return;
            }

            var newText = vm.IconCounterNumerator;
            if (string.Equals(input.Text, newText, StringComparison.Ordinal))
            {
                return;
            }

            var selectionStart = input.SelectionStart;
            var selectionEnd = input.SelectionEnd;
            var caretIndex = input.CaretIndex;
            input.Text = newText;

            if (!preserveSelection)
            {
                input.CaretIndex = input.Text?.Length ?? 0;
                return;
            }

            var safeLength = input.Text?.Length ?? 0;
            input.SelectionStart = Math.Clamp(selectionStart, 0, safeLength);
            input.SelectionEnd = Math.Clamp(selectionEnd, 0, safeLength);
            input.CaretIndex = Math.Clamp(caretIndex, 0, safeLength);
        }

        private void UpdateIconCounterHintVisibility()
        {
            var host = this.FindControl<Border>("iconCounterHost");
            var input = this.FindControl<TextBox>("iconCounterInput");
            var hintUp = this.FindControl<TextBlock>("iconCounterHintUp");
            var hintDown = this.FindControl<TextBlock>("iconCounterHintDown");
            if (hintUp == null || hintDown == null)
            {
                return;
            }

            var shouldShow = _isIconCounterMiddleAdjustActive || (input?.IsVisible == true);
            hintUp.IsVisible = shouldShow;
            hintDown.IsVisible = shouldShow;
        }

        private void AttachWindowLifecycleHandlers()
        {
            var hostWindow = TopLevel.GetTopLevel(this) as Window;
            if (ReferenceEquals(_hostWindow, hostWindow))
            {
                return;
            }

            DetachWindowLifecycleHandlers();
            _hostWindow = hostWindow;
            if (_hostWindow == null)
            {
                return;
            }

            _hostWindow.Deactivated += HostWindow_Deactivated;
            _hostWindow.LostFocus += HostWindow_LostFocus;
        }

        private void DetachWindowLifecycleHandlers()
        {
            if (_hostWindow == null)
            {
                return;
            }

            _hostWindow.Deactivated -= HostWindow_Deactivated;
            _hostWindow.LostFocus -= HostWindow_LostFocus;
            _hostWindow = null;
        }

        private void HostWindow_Deactivated(object? sender, EventArgs e)
        {
            StopIconCounterMiddleAdjustMode();
        }

        private void HostWindow_LostFocus(object? sender, RoutedEventArgs e)
        {
            StopIconCounterMiddleAdjustMode();
        }

        private void SetIconCounterScale(double scale)
        {
            var display = this.FindControl<TextBlock>("iconCounterDisplay");
            if (display?.RenderTransform is ScaleTransform scaleTransform)
            {
                scaleTransform.ScaleX = scale;
                scaleTransform.ScaleY = scale;
            }
        }

        private void EnsureTooltipAlwaysShows(Control? control)
        {
            if (control == null) return;
            control.PointerEntered += (s, e) =>
            {
                var tip = ToolTip.GetTip(control);
                if (tip != null)
                {
                    ToolTip.SetIsOpen(control, true);
                }
            };
            control.PointerExited += (s, e) => ToolTip.SetIsOpen(control, false);
        }

 }
}

