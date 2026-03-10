using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.VisualTree;
using FolderStyleEditorForWindows.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FolderStyleEditorForWindows.Views
{
    public partial class PerformanceMonitor : UserControl
    {
        private const double DragStartThresholdPx = 6;
        private Border? _monitorRoot;
        private ContextMenu? _monitorContextMenu;
        private Visual? _dragHost;
        private bool _isPointerPressed;
        private bool _isDragging;
        private Point _pressedPointInHost;

        public PerformanceMonitor()
        {
            InitializeComponent();
            DataContext = App.Services?.GetRequiredService<PerformanceMonitorViewModel>();
            _monitorRoot = this.FindControl<Border>("MonitorRoot");
            _monitorContextMenu = this.FindControl<ContextMenu>("MonitorContextMenu");
            PointerEntered += PerformanceMonitor_PointerEntered;
            PointerExited += PerformanceMonitor_PointerExited;
            PointerPressed += PerformanceMonitor_PointerPressed;
            PointerReleased += PerformanceMonitor_PointerReleased;
            PointerMoved += PerformanceMonitor_PointerMoved;
        }

        private PerformanceMonitorViewModel? ViewModel => DataContext as PerformanceMonitorViewModel;

        public void SetDragHost(Visual host)
        {
            _dragHost = host;
        }

        public void DismissContextMenu()
        {
            if (_monitorContextMenu != null)
            {
                _monitorContextMenu.Close();
            }
        }

        private void PerformanceMonitor_PointerEntered(object? sender, PointerEventArgs e)
        {
            ApplyVisualState(0.3);
        }

        private void PerformanceMonitor_PointerExited(object? sender, PointerEventArgs e)
        {
            if (_isDragging)
            {
                return;
            }

            ApplyVisualState(0.75);
        }

        private void PerformanceMonitor_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (ViewModel == null)
            {
                return;
            }

            var point = e.GetCurrentPoint(this).Properties;
            if (!point.IsLeftButtonPressed)
            {
                return;
            }

            DismissContextMenu();

            if (TopLevel.GetTopLevel(this) is not TopLevel topLevel)
            {
                return;
            }

            var hostPoint = TryGetPointerPositionInHost(e);
            if (hostPoint == null)
            {
                return;
            }

            _isPointerPressed = true;
            e.Pointer.Capture(this);
            _pressedPointInHost = hostPoint.Value;
            ApplyVisualState(0.3);
            e.Handled = true;
        }

        private void PerformanceMonitor_PointerMoved(object? sender, PointerEventArgs e)
        {
            if ((!_isDragging && !_isPointerPressed) || ViewModel == null)
            {
                return;
            }

            if (TopLevel.GetTopLevel(_dragHost ?? this) is not TopLevel hostTopLevel)
            {
                return;
            }

            var hostPoint = TryGetPointerPositionInHost(e);
            if (hostPoint == null)
            {
                return;
            }

            if (!_isDragging)
            {
                var delta = hostPoint.Value - _pressedPointInHost;
                var movedDistanceSq = (delta.X * delta.X) + (delta.Y * delta.Y);
                if (movedDistanceSq < DragStartThresholdPx * DragStartThresholdPx)
                {
                    return;
                }

                _isDragging = true;
                ViewModel.BeginDrag(_pressedPointInHost);
            }

            ViewModel.UpdateDrag(hostPoint.Value, hostTopLevel.Bounds.Size, Bounds.Size);
            e.Handled = true;
        }

        private void PerformanceMonitor_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_isDragging && !_isPointerPressed)
            {
                return;
            }

            var wasDragging = _isDragging;
            _isDragging = false;
            _isPointerPressed = false;
            e.Pointer.Capture(null);
            if (wasDragging && ViewModel != null && TopLevel.GetTopLevel(_dragHost ?? this) is TopLevel hostTopLevel)
            {
                ViewModel.EndDrag(hostTopLevel.Bounds.Size, Bounds.Size);
            }

            ApplyVisualState(IsPointerOver ? 0.3 : 0.75);
            e.Handled = true;
        }

        private void CloseMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            _isPointerPressed = false;
            _isDragging = false;
            ViewModel?.Hide();
            e.Handled = true;
        }

        private void ApplyVisualState(double opacity)
        {
            if (_monitorRoot == null)
            {
                return;
            }

            _monitorRoot.Opacity = opacity;
        }

        private Point? TryGetPointerPositionInHost(PointerEventArgs e)
        {
            if (_dragHost == null)
            {
                var topLevel = TopLevel.GetTopLevel(this);
                return topLevel == null ? null : e.GetPosition(topLevel);
            }

            var hostTopLevel = TopLevel.GetTopLevel(_dragHost);
            if (hostTopLevel == null)
            {
                return null;
            }

            var screenPoint = this.PointToScreen(e.GetPosition(this));
            return hostTopLevel.PointToClient(screenPoint);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
