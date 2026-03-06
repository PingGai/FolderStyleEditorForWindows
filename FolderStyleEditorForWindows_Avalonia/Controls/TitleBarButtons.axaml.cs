using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Threading.Tasks;

namespace FolderStyleEditorForWindows.Controls
{
    public partial class TitleBarButtons : UserControl
    {
        public static readonly StyledProperty<bool> ShowBackProperty =
            AvaloniaProperty.Register<TitleBarButtons, bool>(nameof(ShowBack));

        public static readonly StyledProperty<bool> ShowMinimizeProperty =
            AvaloniaProperty.Register<TitleBarButtons, bool>(nameof(ShowMinimize), true);

        public static readonly StyledProperty<bool> ShowCloseProperty =
            AvaloniaProperty.Register<TitleBarButtons, bool>(nameof(ShowClose), true);

        public event EventHandler<RoutedEventArgs>? BackRequested;

        public bool ShowBack
        {
            get => GetValue(ShowBackProperty);
            set => SetValue(ShowBackProperty, value);
        }

        public bool ShowMinimize
        {
            get => GetValue(ShowMinimizeProperty);
            set => SetValue(ShowMinimizeProperty, value);
        }

        public bool ShowClose
        {
            get => GetValue(ShowCloseProperty);
            set => SetValue(ShowCloseProperty, value);
        }

        public TitleBarButtons()
        {
            InitializeComponent();
        }

        private void BackButton_Click(object? sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, e);
        }

        private async void MinimizeButton_Click(object? sender, RoutedEventArgs e)
        {
            if (VisualRoot is MainWindow mainWindow)
            {
                await mainWindow.AnimateFadeOut();
                mainWindow.WindowState = WindowState.Minimized;
            }
        }

        private async void CloseButton_Click(object? sender, RoutedEventArgs e)
        {
            if (VisualRoot is MainWindow mainWindow)
            {
                await mainWindow.AnimateFadeOut();
                mainWindow.Close();
            }
        }
    }
}
