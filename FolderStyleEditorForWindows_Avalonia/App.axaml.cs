using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FolderStyleEditorForWindows.Services;
using FolderStyleEditorForWindows.ViewModels;
using FolderStyleEditorForWindows.Views;
using Microsoft.Extensions.DependencyInjection;

namespace FolderStyleEditorForWindows;

public partial class App : Application
{
    public static ServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IToastService, ToastService>();
        services.AddSingleton<HoverIconViewModel>();
        services.AddSingleton<HoverIconService>();
        services.AddSingleton<MainViewModel>();
        Services = services.BuildServiceProvider();
        
        // Load and apply SVG color from config
        const string hardcodedColor = "#ff606064";
        var colorFromConfig = ConfigManager.Config.Appearance?.SvgDefaultColor;
        
        Avalonia.Media.Color color;
        try
        {
            color = !string.IsNullOrEmpty(colorFromConfig) ? Avalonia.Media.Color.Parse(colorFromConfig) : Avalonia.Media.Color.Parse(hardcodedColor);
        }
        catch
        {
            color = Avalonia.Media.Color.Parse(hardcodedColor);
        }

        string cssHex = color.A == 0xFF
            ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
            : $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";
        
        string svgCss = $"svg {{ color: {cssHex}; }}";
        
        Current.Resources["SvgCss"] = svgCss;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainViewModel = Services.GetRequiredService<MainViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            desktop.MainWindow.Loaded += (sender, args) =>
            {
                var editSessionManager = new EditSessionManager(mainViewModel);
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}