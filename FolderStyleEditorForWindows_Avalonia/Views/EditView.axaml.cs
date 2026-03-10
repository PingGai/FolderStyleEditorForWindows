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
using System.Windows.Input;
using FolderStyleEditorForWindows;
using FolderStyleEditorForWindows.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FolderStyleEditorForWindows.Views
{
    public partial class EditView : UserControl, IDisposable
    {
        private const int TitleGradientCycles = 3;
        private bool _suppressAliasAutocompleteDismissOnce;
        private readonly DispatcherTimer _iconListScrollTimer;
        private readonly AmbientAnimationScheduler? _ambientScheduler;
        private readonly FrameRateSettings? _frameRateSettings;
        private IAmbientAnimationHandle? _adminTitleAmbientHandle;
        private ScrollViewer? _editScrollViewer;
        private ScrollViewer? _iconListScrollViewer;
        private ScrollViewer? _aliasAutocompleteScrollViewer;
        private VerticalScrollViewerAnimatedBehavior? _editScrollAnimatedBehavior;
        private VerticalScrollViewerAnimatedBehavior? _iconListScrollAnimatedBehavior;
        private VerticalScrollViewerAnimatedBehavior? _aliasAutocompleteScrollAnimatedBehavior;
        private double _iconListTargetOffsetY;
        private bool _pendingInitialScrollReset;
        private LinearGradientBrush? _titleGoldBrush;
        private TextBlock? _editTitleText;
        private double _titleGradientPhase;
        private double _lastTitleAmbientTickSeconds;
        private bool _isAdminTitleAmbientEnabled;
        private bool _isAmbientSuspended;
        private ElevationSessionState? _elevationSessionState;
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

            _iconListScrollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _iconListScrollTimer.Tick += IconListScrollTimer_Tick;
            _ambientScheduler = App.Services?.GetService<AmbientAnimationScheduler>();
            _frameRateSettings = App.Services?.GetService<FrameRateSettings>();
            if (_ambientScheduler != null)
            {
                _adminTitleAmbientHandle = _ambientScheduler.Register(
                    "edit-admin-title-gradient",
                    () => _frameRateSettings?.AdminTitleAmbientFps ?? 15,
                    OnAdminTitleAmbientTick);
                    _adminTitleAmbientHandle.SetEnabled(false);
            }

            DebugRuntimeAnalysis.PauseAnimationsChanged += DebugRuntimeAnalysis_PauseAnimationsChanged;

            if (_frameRateSettings != null)
            {
                _frameRateSettings.PropertyChanged += FrameRateSettings_PropertyChanged;
            }
        }

        public void Dispose()
        {
            UnsubscribeFromViewModel();
            _adminTitleAmbientHandle?.Dispose();
            _adminTitleAmbientHandle = null;
            DebugRuntimeAnalysis.PauseAnimationsChanged -= DebugRuntimeAnalysis_PauseAnimationsChanged;
            if (_frameRateSettings != null)
            {
                _frameRateSettings.PropertyChanged -= FrameRateSettings_PropertyChanged;
            }

            if (DataContext is ViewModels.MainViewModel vm)
            {
                vm.ClearIconPreview();
            }

            var iconListBox = this.FindControl<ListBox>("iconListBox");
            if (iconListBox != null)
            {
                iconListBox.LayoutUpdated -= IconListBox_LayoutUpdated;
            }
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
                EnsureTooltipAlwaysShows(iconCounterHost);
            }

            var iconCounterInput = this.FindControl<TextBox>("iconCounterInput");
            if (iconCounterInput != null)
            {
                iconCounterInput.PointerWheelChanged += IconCounterDisplay_PointerWheelChanged;
                iconCounterInput.KeyDown += IconCounterInput_KeyDown;
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

            var iconListBox = this.FindControl<ListBox>("iconListBox");
            _iconListScrollViewer = this.FindControl<ScrollViewer>("iconListScrollViewer");
            _aliasAutocompleteScrollViewer = this.FindControl<ScrollViewer>("aliasAutocompleteScrollViewer");
            if (iconListBox != null)
            {
                iconListBox.SelectionChanged -= IconListBox_SelectionChanged;
                iconListBox.SelectionChanged += IconListBox_SelectionChanged;
                iconListBox.RemoveHandler(InputElement.KeyDownEvent, IconListBox_KeyDown);
                iconListBox.AddHandler(InputElement.KeyDownEvent, IconListBox_KeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
                iconListBox.PointerPressed -= IconListBox_PointerPressed;
                iconListBox.PointerPressed += IconListBox_PointerPressed;
                Dispatcher.UIThread.Post(UpdateIconPreviewVisuals, DispatcherPriority.Loaded);
            }

            ResetEditScrollToTop();
            Dispatcher.UIThread.Post(ResetEditScrollToTop, DispatcherPriority.Loaded);
            ApplyScrollAnimationDebugState();
            UpdateAdminAmbientState();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            UnsubscribeFromViewModel();
            if (_elevationSessionState != null)
            {
                _elevationSessionState.PropertyChanged -= ElevationSessionState_PropertyChanged;
            }
            _adminTitleAmbientHandle?.SetEnabled(false);
            base.OnDetachedFromVisualTree(e);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == IsVisibleProperty)
            {
                UpdateAdminAmbientState();
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
        }

        private void FrameRateSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FrameRateSettings.DisableEditScrollAnimations))
            {
                Dispatcher.UIThread.Post(ApplyScrollAnimationDebugState, DispatcherPriority.Background);
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
                BeginIconCounterEdit(vm);
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
            if (e.Key == Key.Enter && DataContext is ViewModels.MainViewModel vm)
            {
                CommitIconCounterInput(sender as TextBox, vm);
                e.Handled = true;
            }
        }

        private void IconCounterInput_LostFocus(object? sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                CommitIconCounterInput(sender as TextBox, vm);
            }
        }

        private void CommitIconCounterInput(TextBox? textBox, ViewModels.MainViewModel vm)
        {
            if (textBox == null)
            {
                EndIconCounterEdit();
                return;
            }
            if (!vm.IsIconCounterVisible || !vm.IconCounterDenominator.Any(char.IsDigit))
            {
                textBox.Text = vm.IconCounterNumerator;
                EndIconCounterEdit();
                return;
            }

            if (int.TryParse(textBox.Text, out var value))
            {
                vm.JumpToIconIndex(value);
            }
            else
            {
                textBox.Text = vm.IconCounterNumerator;
            }
            EndIconCounterEdit();
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

        private void DebugRuntimeAnalysis_PauseAnimationsChanged(object? sender, EventArgs e)
        {
            Dispatcher.UIThread.Post(UpdateAdminAmbientState, DispatcherPriority.Background);
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

        private void IconListBox_LayoutUpdated(object? sender, EventArgs e)
        {
            UpdateIconPreviewVisuals();
        }

        private void IconListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox && DataContext is ViewModels.MainViewModel vm)
            {
                var editScrollViewer = this.FindControl<ScrollViewer>("editScrollViewer");
                var preservedEditOffset = editScrollViewer?.Offset.Y ?? 0;

                if (listBox.SelectedItem is ViewModels.IconViewModel selectedIcon)
                {
                    vm.SelectedIcon = selectedIcon;
                }

                UpdateIconPreviewVisuals();
                Dispatcher.UIThread.Post(() =>
                {
                    listBox.Focus();
                    if (editScrollViewer != null)
                    {
                        editScrollViewer.Offset = new Vector(editScrollViewer.Offset.X, preservedEditOffset);
                    }
                }, DispatcherPriority.Input);
            }
        }

        private void IconListBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (sender is not ListBox listBox || DataContext is not ViewModels.MainViewModel vm || vm.Icons.Count == 0)
            {
                return;
            }

            var currentAnchor = vm.PreviewedIcon ?? vm.SelectedIcon;
            var currentIndex = currentAnchor != null ? vm.Icons.IndexOf(currentAnchor) : 0;
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            // 当前布局固定为每行 5 个图标。如果图标单元尺寸或容器宽度后续调整，这里要同步修改。
            const int columns = 5;
            var nextIndex = currentIndex;

            switch (e.Key)
            {
                case Key.Left:
                    nextIndex = Math.Max(0, currentIndex - 1);
                    break;
                case Key.Right:
                    nextIndex = Math.Min(vm.Icons.Count - 1, currentIndex + 1);
                    break;
                case Key.Up:
                    nextIndex = currentIndex < columns ? currentIndex : currentIndex - columns;
                    break;
                case Key.Down:
                    nextIndex = Math.Min(vm.Icons.Count - 1, currentIndex + columns);
                    break;
                case Key.Enter:
                    if (vm.PreviewedIcon != null)
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
                UpdateIconPreviewVisuals();
                SmoothScrollIconPreviewIntoView();
            }
        }

        private void UpdateIconPreviewVisuals()
        {
            var iconListBox = this.FindControl<ListBox>("iconListBox");
            if (iconListBox == null)
            {
                return;
            }

            foreach (var item in iconListBox.GetVisualDescendants().OfType<ListBoxItem>())
            {
                var shouldPreview = item.DataContext is ViewModels.IconViewModel icon && icon.IsPreviewed && !icon.IsSelected;
                item.Classes.Set("previewed", shouldPreview);
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

            if (e.PropertyName == nameof(ViewModels.MainViewModel.SelectedIcon) ||
                e.PropertyName == nameof(ViewModels.MainViewModel.PreviewedIcon))
            {
                Dispatcher.UIThread.Post(UpdateIconPreviewVisuals, DispatcherPriority.Background);
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

        private void SmoothScrollIconPreviewIntoView()
        {
            if (_iconListScrollViewer == null)
            {
                return;
            }

            var previewContainer = _iconListScrollViewer
                .GetVisualDescendants()
                .OfType<ListBoxItem>()
                .FirstOrDefault(x => x.IsPointerOver || x.IsFocused);

            if (previewContainer == null)
            {
                previewContainer = _iconListScrollViewer
                    .GetVisualDescendants()
                    .OfType<ListBoxItem>()
                    .FirstOrDefault(x => x.DataContext is ViewModels.IconViewModel icon && icon.IsPreviewed);
            }

            if (previewContainer == null)
            {
                return;
            }

            var relativeTopLeft = previewContainer.TranslatePoint(default, _iconListScrollViewer);
            if (relativeTopLeft == null)
            {
                return;
            }

            var currentOffset = _iconListScrollViewer.Offset;
            var top = relativeTopLeft.Value.Y;
            var bottom = top + previewContainer.Bounds.Height;
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

            if (!_iconListScrollTimer.IsEnabled)
            {
                _iconListScrollTimer.Start();
            }
        }

        private void IconListScrollTimer_Tick(object? sender, EventArgs e)
        {
            if (_iconListScrollViewer == null)
            {
                _iconListScrollTimer.Stop();
                return;
            }

            var currentOffset = _iconListScrollViewer.Offset;
            var nextY = currentOffset.Y + ((_iconListTargetOffsetY - currentOffset.Y) * 0.28);

            if (Math.Abs(nextY - _iconListTargetOffsetY) < 0.5)
            {
                nextY = _iconListTargetOffsetY;
                _iconListScrollTimer.Stop();
            }

            _iconListScrollViewer.Offset = new Vector(currentOffset.X, nextY);
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

            if (target.FindAncestorOfType<ListBox>() is ListBox listBox && listBox.Name == "iconListBox")
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

        private void BeginIconCounterEdit(ViewModels.MainViewModel vm)
        {
            var display = this.FindControl<TextBlock>("iconCounterDisplay");
            var input = this.FindControl<TextBox>("iconCounterInput");
            if (display == null || input == null) return;

            display.IsVisible = false;
            input.IsVisible = true;
            input.Text = vm.IconCounterNumerator;
            input.CaretIndex = input.Text?.Length ?? 0;
            input.Focus();
            input.SelectAll();
        }

        private void EndIconCounterEdit()
        {
            var display = this.FindControl<TextBlock>("iconCounterDisplay");
            var input = this.FindControl<TextBox>("iconCounterInput");
            if (display == null || input == null) return;

            input.IsVisible = false;
            display.IsVisible = true;
            SetIconCounterScale(1.0);
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

