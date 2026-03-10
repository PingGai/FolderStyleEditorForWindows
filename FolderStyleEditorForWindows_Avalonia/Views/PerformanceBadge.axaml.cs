using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace FolderStyleEditorForWindows.Views
{
    public partial class PerformanceBadge : UserControl
    {
        public static readonly StyledProperty<string> LabelProperty =
            AvaloniaProperty.Register<PerformanceBadge, string>(nameof(Label), string.Empty);

        public static readonly StyledProperty<string> ValueTextProperty =
            AvaloniaProperty.Register<PerformanceBadge, string>(nameof(ValueText), "0");

        private Border? _badgeRoot;

        public PerformanceBadge()
        {
            InitializeComponent();
            _badgeRoot = this.FindControl<Border>("BadgeRoot");
            PointerEntered += PerformanceBadge_PointerEntered;
            PointerExited += PerformanceBadge_PointerExited;
            PointerPressed += PerformanceBadge_PointerPressed;
            PointerReleased += PerformanceBadge_PointerReleased;
        }

        public string Label
        {
            get => GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public string ValueText
        {
            get => GetValue(ValueTextProperty);
            set => SetValue(ValueTextProperty, value);
        }

        private void PerformanceBadge_PointerEntered(object? sender, PointerEventArgs e)
        {
            ApplyVisualState(0.85, 1.02);
        }

        private void PerformanceBadge_PointerExited(object? sender, PointerEventArgs e)
        {
            ApplyVisualState(0.6, 1.0);
        }

        private void PerformanceBadge_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                ApplyVisualState(0.85, 0.98);
            }
        }

        private void PerformanceBadge_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            ApplyVisualState(IsPointerOver ? 0.85 : 0.6, IsPointerOver ? 1.02 : 1.0);
        }

        private void ApplyVisualState(double opacity, double scale)
        {
            if (_badgeRoot == null)
            {
                return;
            }

            _badgeRoot.Opacity = opacity;
            if (_badgeRoot.RenderTransform is ScaleTransform scaleTransform)
            {
                scaleTransform.ScaleX = scale;
                scaleTransform.ScaleY = scale;
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
