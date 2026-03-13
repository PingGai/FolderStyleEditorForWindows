using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FolderStyleEditorForWindows.Services;
using System.ComponentModel;
using System.Collections.Generic;
using System.Windows.Input;
using System.Threading.Tasks;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Microsoft.Extensions.DependencyInjection;
using SvgControl = Avalonia.Svg.Skia.Svg;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace FolderStyleEditorForWindows.Views
{
    public partial class InterruptDialog : UserControl
    {
        private const double MaxShakeAmplitude = 2.8;
        private const double ShakeDecayDurationMs = 500;
        private readonly DispatcherTimer _dangerShakeTimer;
        private readonly DispatcherTimer _codeBlockScrollTimer;
        private readonly Random _random = new();
        private Button? _primaryButton;
        private InterruptDialogState? _state;
        private Border? _dialogCard;
        private ItemsControl? _passiveChoiceCardsHost;
        private Border? _codeBlockHost;
        private SelectableTextBlock? _codeBlockTextBox;
        private ScrollViewer? _codeBlockScrollViewer;
        private TranslateTransform? _primaryButtonShakeTransform;
        private double _shakeStrength;
        private bool _isDangerHovered;
        private DateTime _codeBlockPointerPressedAt = DateTime.MinValue;
        private bool _codeBlockPointerMoved;
        private bool _codeBlockClickArmed;
        private Point _codeBlockPointerPressedPosition;
        private double _codeBlockScrollTargetY;
        private readonly Dictionary<Control, PropertyChangedEventHandler> _animatedContainerHandlers = new();
        private int _headerMetaTapCount;

        public InterruptDialog()
        {
            InitializeComponent();

            _dangerShakeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(45)
            };
            _dangerShakeTimer.Tick += DangerShakeTimer_Tick;
            _codeBlockScrollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _codeBlockScrollTimer.Tick += CodeBlockScrollTimer_Tick;

            _primaryButton = this.FindControl<Button>("PrimaryButton");
            _dialogCard = this.FindControl<Border>("DialogCard");
            _passiveChoiceCardsHost = this.FindControl<ItemsControl>("PassiveChoiceCardsHost");
            _codeBlockHost = this.FindControl<Border>("DialogCodeBlockHost");
            _codeBlockTextBox = this.FindControl<SelectableTextBlock>("DialogCodeBlockTextBox");
            _codeBlockScrollViewer = this.FindControl<ScrollViewer>("DialogCodeBlockScrollViewer");
            if (_codeBlockHost != null)
            {
                _codeBlockHost.AddHandler(PointerPressedEvent, CodeBlockHost_PointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
                _codeBlockHost.AddHandler(PointerMovedEvent, CodeBlockHost_PointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
                _codeBlockHost.AddHandler(PointerReleasedEvent, CodeBlockHost_PointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
                _codeBlockHost.AddHandler(Control.ContextRequestedEvent, CodeBlockHost_ContextRequested, RoutingStrategies.Tunnel, handledEventsToo: true);
            }
            if (_codeBlockScrollViewer != null)
            {
                _codeBlockScrollViewer.AddHandler(PointerWheelChangedEvent, CodeBlockScrollViewer_PointerWheelChanged, RoutingStrategies.Tunnel, handledEventsToo: true);
            }
            if (_primaryButton != null)
            {
                _primaryButton.PointerEntered += PrimaryButton_PointerEntered;
                _primaryButton.PointerExited += PrimaryButton_PointerExited;
            }

            AddHandler(KeyDownEvent, InterruptDialog_KeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
            DataContextChanged += InterruptDialog_DataContextChanged;
            LayoutUpdated += InterruptDialog_LayoutUpdated;
        }

        private void InterruptDialog_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && DataContext is InterruptDialogState { IsActive: true } state && state.DismissOnEsc)
            {
                if (state.CancelCommand.CanExecute(null))
                {
                    state.CancelCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }

        private void OverlayMask_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (DataContext is not InterruptDialogState { IsActive: true, AllowOverlayClickDismiss: true } state)
            {
                return;
            }

            if (state.CancelCommand.CanExecute(null))
            {
                state.CancelCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void InterruptDialog_DataContextChanged(object? sender, EventArgs e)
        {
            if (_state != null)
            {
                _state.PropertyChanged -= State_PropertyChanged;
            }

            _state = DataContext as InterruptDialogState;
            if (_state != null)
            {
                _state.PropertyChanged += State_PropertyChanged;
            }

            UpdatePrimaryButtonShakeState();
            SyncDialogActiveState();
            _headerMetaTapCount = 0;
            UpdatePassiveChoiceBounds();
        }

        private void PrimaryButton_PointerEntered(object? sender, PointerEventArgs e)
        {
            if (DataContext is InterruptDialogState { IsPrimaryDanger: true })
            {
                EnsurePrimaryButtonTransform();
                _isDangerHovered = true;
                if (!_dangerShakeTimer.IsEnabled)
                {
                    _dangerShakeTimer.Start();
                }
            }
        }

        private void PrimaryButton_PointerExited(object? sender, PointerEventArgs e)
        {
            _isDangerHovered = false;
        }

        private void DangerShakeTimer_Tick(object? sender, EventArgs e)
        {
            if (_primaryButton == null || DataContext is not InterruptDialogState { IsPrimaryDanger: true })
            {
                ResetShakeImmediately();
                return;
            }

            EnsurePrimaryButtonTransform();

            if (_isDangerHovered)
            {
                _shakeStrength += (1 - _shakeStrength) * 0.22;
            }
            else
            {
                _shakeStrength = Math.Max(0, _shakeStrength - (_dangerShakeTimer.Interval.TotalMilliseconds / ShakeDecayDurationMs));
            }

            var amplitude = MaxShakeAmplitude * _shakeStrength;

            if (_primaryButtonShakeTransform != null)
            {
                _primaryButtonShakeTransform.X = (_random.NextDouble() - 0.5) * 2.0 * amplitude;
                _primaryButtonShakeTransform.Y = (_random.NextDouble() - 0.5) * 2.0 * amplitude;
            }

            if (!_isDangerHovered && _shakeStrength <= 0.001)
            {
                ResetShakeImmediately();
            }
        }

        private void UpdatePrimaryButtonShakeState()
        {
            if (DataContext is not InterruptDialogState { IsPrimaryDanger: true })
            {
                ResetShakeImmediately();
            }
        }

        private void State_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(InterruptDialogState.IsActive) ||
                e.PropertyName == nameof(InterruptDialogState.IsPrimaryDanger) ||
                e.PropertyName == nameof(InterruptDialogState.IsPassiveOverlay) ||
                e.PropertyName == nameof(InterruptDialogState.PassiveChoiceCards))
            {
                SyncDialogActiveState();
                UpdatePassiveChoiceBounds();
            }
        }

        private void InterruptDialog_LayoutUpdated(object? sender, EventArgs e)
        {
            UpdatePassiveChoiceBounds();
        }

        private void UpdatePassiveChoiceBounds()
        {
            var interruptDialogService = App.Services?.GetService<InterruptDialogService>();
            if (interruptDialogService == null || _state == null || _passiveChoiceCardsHost == null)
            {
                return;
            }

            if (!_state.IsPassiveOverlayVisible || !_state.HasPassiveChoiceCards || !_passiveChoiceCardsHost.IsVisible)
            {
                interruptDialogService.UpdatePassiveChoiceBounds(null);
                return;
            }

            var visualRoot = this.GetVisualRoot() as Visual;
            if (visualRoot == null)
            {
                interruptDialogService.UpdatePassiveChoiceBounds(null);
                return;
            }

            var topLeft = _passiveChoiceCardsHost.TranslatePoint(new Point(0, 0), visualRoot);
            if (!topLeft.HasValue)
            {
                interruptDialogService.UpdatePassiveChoiceBounds(null);
                return;
            }

            var bounds = new Rect(topLeft.Value, _passiveChoiceCardsHost.Bounds.Size);
            interruptDialogService.UpdatePassiveChoiceBounds(bounds);
        }

        private void ResetShakeImmediately()
        {
            _dangerShakeTimer.Stop();
            _isDangerHovered = false;
            _shakeStrength = 0;

            if (_primaryButtonShakeTransform != null)
            {
                _primaryButtonShakeTransform.X = 0;
                _primaryButtonShakeTransform.Y = 0;
            }
        }

        private void EnsurePrimaryButtonTransform()
        {
            if (_primaryButton == null)
            {
                return;
            }

            if (_primaryButtonShakeTransform != null)
            {
                return;
            }

            switch (_primaryButton.RenderTransform)
            {
                case TransformGroup existingGroup:
                    _primaryButtonShakeTransform = new TranslateTransform();
                    existingGroup.Children.Add(_primaryButtonShakeTransform);
                    break;
                case null:
                    _primaryButtonShakeTransform = new TranslateTransform();
                    _primaryButton.RenderTransform = _primaryButtonShakeTransform;
                    break;
                default:
                    _primaryButtonShakeTransform = new TranslateTransform();
                    var transformGroup = new TransformGroup();
                    if (_primaryButton.RenderTransform is Transform originalTransform)
                    {
                        transformGroup.Children.Add(originalTransform);
                    }

                    transformGroup.Children.Add(_primaryButtonShakeTransform);
                    _primaryButton.RenderTransform = transformGroup;
                    break;
            }
        }

        private void FocusPrimaryButton()
        {
            if (_primaryButton == null)
            {
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                if (_primaryButton.IsEffectivelyVisible && _primaryButton.GetVisualRoot() != null)
                {
                    _primaryButton.Focus();
                }
            }, DispatcherPriority.Input);
        }

        private void SyncDialogActiveState()
        {
            if (_state is not { IsActive: true })
            {
                ResetShakeImmediately();
                return;
            }

            FocusPrimaryButton();

            if (_state.IsPrimaryDanger && _primaryButton != null)
            {
                _isDangerHovered = _primaryButton.IsPointerOver;
                if (_isDangerHovered && !_dangerShakeTimer.IsEnabled)
                {
                    EnsurePrimaryButtonTransform();
                    _dangerShakeTimer.Start();
                }
            }
        }

        private void ExpandableSectionContent_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Control host ||
                host.DataContext is not DialogExpandableSectionItem section ||
                !section.IsExpanded)
            {
                return;
            }

            if (e.Source is Control source && HasInteractiveAncestor(source, host))
            {
                return;
            }

            section.IsExpanded = false;
            e.Handled = true;
        }

        private void LicenseTextContainer_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Control host ||
                host.DataContext is not DialogLicenseItem item ||
                !item.IsTextExpanded)
            {
                return;
            }

            if (e.Source is Control source && HasInteractiveAncestor(source, host))
            {
                return;
            }

            item.IsTextExpanded = false;
            e.Handled = true;
        }

        private static bool HasInteractiveAncestor(Control source, Control host)
        {
            return source != host &&
                   (source is Button or TextBlock or TextBox or ScrollViewer or ToggleButton or SvgControl ||
                    source.FindAncestorOfType<Button>() != null ||
                    source.FindAncestorOfType<TextBox>() != null ||
                    source.FindAncestorOfType<ScrollViewer>() != null ||
                    source.FindAncestorOfType<SvgControl>() != null ||
                    source.FindAncestorOfType<ToggleButton>() != null);
        }

        private void StatusFieldCard_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
                sender is Control control &&
                control.DataContext is DialogStatusFieldItem { HasCommand: true, Command: { } command } &&
                command.CanExecute(null))
            {
                command.Execute(null);
                e.Handled = true;
            }
        }

        private void AnimatedContainer_AttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
        {
            if (sender is not Border border)
            {
                return;
            }

            PropertyChangedEventHandler handler = (_, args) =>
            {
                if (args.PropertyName == nameof(DialogExpandableSectionItem.IsExpanded) ||
                    args.PropertyName == nameof(DialogLicenseItem.IsTextExpanded))
                {
                    ApplyAnimatedContainerState(border, immediate: false);

                    if (_dialogCard != null)
                    {
                        _dialogCard.InvalidateMeasure();
                        _dialogCard.InvalidateArrange();
                    }
                }
            };

            switch (border.DataContext)
            {
                case DialogExpandableSectionItem section:
                    section.PropertyChanged += handler;
                    _animatedContainerHandlers[border] = handler;
                    ApplyAnimatedContainerState(border, immediate: true);
                    break;
                case DialogLicenseItem item:
                    item.PropertyChanged += handler;
                    _animatedContainerHandlers[border] = handler;
                    ApplyAnimatedContainerState(border, immediate: true);
                    break;
            }
        }

        private void AnimatedContainer_DetachedFromVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
        {
            if (sender is not Border border || !_animatedContainerHandlers.Remove(border, out var handler))
            {
                return;
            }

            switch (border.DataContext)
            {
                case DialogExpandableSectionItem section:
                    section.PropertyChanged -= handler;
                    break;
                case DialogLicenseItem item:
                    item.PropertyChanged -= handler;
                    break;
            }
        }

        private static void ApplyAnimatedContainerState(Border border, bool immediate)
        {
            var expanded = border.DataContext switch
            {
                DialogExpandableSectionItem section => section.IsExpanded,
                DialogLicenseItem item => item.IsTextExpanded,
                _ => false
            };

            border.IsHitTestVisible = expanded;
            border.MaxHeight = expanded ? 100000 : 0;
            border.Opacity = expanded ? 1 : 0;

            if (border.RenderTransform is ScaleTransform scaleTransform)
            {
                scaleTransform.ScaleY = expanded ? 1 : 0.96;
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void CodeBlockHost_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            {
                _codeBlockClickArmed = false;
                if (_codeBlockScrollViewer?.ContextMenu != null)
                {
                    _codeBlockScrollViewer.ContextMenu.Open(_codeBlockScrollViewer);
                    e.Handled = true;
                }
                return;
            }

            _codeBlockPointerPressedAt = DateTime.UtcNow;
            _codeBlockPointerMoved = false;
            _codeBlockClickArmed = true;
            _codeBlockPointerPressedPosition = e.GetPosition(_codeBlockScrollViewer);
        }

        private void CodeBlockHost_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (_codeBlockScrollViewer == null)
            {
                _codeBlockPointerMoved = true;
                return;
            }

            var currentPosition = e.GetPosition(_codeBlockScrollViewer);
            if (Math.Abs(currentPosition.X - _codeBlockPointerPressedPosition.X) > 4 ||
                Math.Abs(currentPosition.Y - _codeBlockPointerPressedPosition.Y) > 4)
            {
                _codeBlockPointerMoved = true;
                _codeBlockClickArmed = false;
            }
        }

        private void CodeBlockScrollViewer_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (_codeBlockScrollViewer == null)
            {
                return;
            }

            var current = _codeBlockScrollViewer.Offset.Y;
            var maxOffset = Math.Max(0, _codeBlockScrollViewer.Extent.Height - _codeBlockScrollViewer.Viewport.Height);
            var delta = e.Delta.Y * 56;
            _codeBlockScrollTargetY = Math.Clamp(current - delta, 0, maxOffset);
            if (!_codeBlockScrollTimer.IsEnabled)
            {
                _codeBlockScrollTimer.Start();
            }

            e.Handled = true;
        }

        private void CodeBlockHost_ContextRequested(object? sender, ContextRequestedEventArgs e)
        {
            if (_codeBlockHost?.ContextMenu == null)
            {
                return;
            }

            _codeBlockHost.ContextMenu.Open(_codeBlockHost);
            e.Handled = true;
        }

        private void CodeBlockScrollTimer_Tick(object? sender, EventArgs e)
        {
            if (_codeBlockScrollViewer == null)
            {
                _codeBlockScrollTimer.Stop();
                return;
            }

            var current = _codeBlockScrollViewer.Offset;
            var nextY = current.Y + ((_codeBlockScrollTargetY - current.Y) * 0.26);
            if (Math.Abs(nextY - _codeBlockScrollTargetY) < 0.4)
            {
                nextY = _codeBlockScrollTargetY;
                _codeBlockScrollTimer.Stop();
            }

            _codeBlockScrollViewer.Offset = new Vector(current.X, nextY);
        }

        private async void DialogHeaderMetaText_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            e.Handled = true;
            if (_state is not { IsActive: true })
            {
                return;
            }

            var loc = LocalizationManager.Instance;
            if (!string.Equals(_state.HeaderMeta, loc["Home_AboutDialog_Publisher"], StringComparison.Ordinal))
            {
                return;
            }

            _headerMetaTapCount++;
            if (_headerMetaTapCount < 5)
            {
                return;
            }

            _headerMetaTapCount = 0;
            _state.CancelCommand.Execute(null);

            var dialogService = App.Services?.GetRequiredService<InterruptDialogService>();
            if (dialogService == null)
            {
                return;
            }

            await Task.Delay(220);
            await dialogService.ShowDebugDialogAsync();
        }

        private async void CodeBlockHost_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_state?.CodeBlock is not DialogCodeBlockItem codeBlock)
            {
                return;
            }

            if (e.InitialPressMouseButton != MouseButton.Left)
            {
                return;
            }

            if (!_codeBlockClickArmed)
            {
                return;
            }

            _codeBlockClickArmed = false;

            if (_codeBlockPointerMoved || DateTime.UtcNow - _codeBlockPointerPressedAt > TimeSpan.FromMilliseconds(220))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(_codeBlockTextBox?.SelectedText))
            {
                return;
            }

            await CopyCodeBlockContentAsync(codeBlock.Content);
        }

        private async void CodeBlockCopyMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            if (_state?.CodeBlock is DialogCodeBlockItem codeBlock)
            {
                await CopyCodeBlockContentAsync(codeBlock.Content);
            }
        }

        private void CodeBlockSelectAllMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            _codeBlockTextBox?.SelectAll();
        }

        private static async Task CopyCodeBlockContentAsync(string content)
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
                desktop.MainWindow?.Clipboard == null)
            {
                return;
            }

            await desktop.MainWindow.Clipboard.SetTextAsync(content);
            var toastService = App.Services?.GetRequiredService<IToastService>();
            toastService?.Show(LocalizationManager.Instance["Toast_CopySuccess"], new SolidColorBrush(Color.Parse("#EBB762")));
        }

        private void CropResetHint_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
                e.ClickCount >= 2 &&
                sender is Control control &&
                control.DataContext is DialogImageCropFieldItem { ResetCropCommand: { } command } &&
                command.CanExecute(null))
            {
                command.Execute(null);
                e.Handled = true;
            }
        }
    }
}
