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
using Avalonia.Controls.Primitives;
using SvgControl = Avalonia.Svg.Skia.Svg;

namespace FolderStyleEditorForWindows.Views
{
    public partial class InterruptDialog : UserControl
    {
        private const double MaxShakeAmplitude = 2.8;
        private const double ShakeDecayDurationMs = 500;
        private readonly DispatcherTimer _dangerShakeTimer;
        private readonly Random _random = new();
        private Button? _primaryButton;
        private InterruptDialogState? _state;
        private Border? _dialogCard;
        private TranslateTransform? _primaryButtonShakeTransform;
        private double _shakeStrength;
        private bool _isDangerHovered;
        private readonly Dictionary<Control, PropertyChangedEventHandler> _animatedContainerHandlers = new();

        public InterruptDialog()
        {
            InitializeComponent();

            _dangerShakeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(45)
            };
            _dangerShakeTimer.Tick += DangerShakeTimer_Tick;

            _primaryButton = this.FindControl<Button>("PrimaryButton");
            _dialogCard = this.FindControl<Border>("DialogCard");
            if (_primaryButton != null)
            {
                _primaryButton.PointerEntered += PrimaryButton_PointerEntered;
                _primaryButton.PointerExited += PrimaryButton_PointerExited;
            }

            AddHandler(KeyDownEvent, InterruptDialog_KeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
            DataContextChanged += InterruptDialog_DataContextChanged;
        }

        private void InterruptDialog_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && DataContext is InterruptDialogState { IsActive: true } state)
            {
                if (state.CancelCommand.CanExecute(null))
                {
                    state.CancelCommand.Execute(null);
                    e.Handled = true;
                }
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
                e.PropertyName == nameof(InterruptDialogState.IsPrimaryDanger))
            {
                SyncDialogActiveState();
            }
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
    }
}
