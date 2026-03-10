using System;
using System.Globalization;
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
        services.AddSingleton<FrameRateSettings>(sp =>
        {
            var settings = new FrameRateSettings();
            settings.LoadFromConfig(ConfigManager.Config);
            return settings;
        });
        services.AddSingleton<DisplayInfoService>();
        services.AddSingleton<AnimationStateSource>();
        services.AddSingleton<FrameRateGovernor>();
        services.AddSingleton<PerformanceTelemetryService>();
        services.AddSingleton<PerformanceMonitorSessionState>();
        services.AddSingleton<PerformanceMonitorViewModel>();
        services.AddSingleton<ComponentFpsBadgeSource>();
        services.AddSingleton<AmbientAnimationScheduler>();
        services.AddSingleton<RenderScheduler>();
        services.AddSingleton<LayerInvalidationController>();
        services.AddSingleton<IToastService, ToastService>();
        services.AddSingleton<HoverIconViewModel>();
        services.AddSingleton<HoverIconService>();
        services.AddSingleton<DragIntentAnalyzerService>();
        services.AddSingleton<ElevationSessionState>();
        services.AddSingleton<LicenseCatalogService>();
        services.AddSingleton<InterruptDialogService>();
        services.AddSingleton<FolderStyleMutationEngine>();
        services.AddSingleton<ElevatedHelperController>();
        services.AddSingleton<FolderStyleSaveCoordinator>();
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

        string cssHex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        
        var svgOpacity = ConfigManager.Config.Debug?.SvgOpacity ?? 0.75;
        if (double.IsNaN(svgOpacity) || double.IsInfinity(svgOpacity))
        {
            svgOpacity = 0.75;
        }

        svgOpacity = Math.Clamp(svgOpacity, 0.0, 1.0);

        string svgCss = string.Join(" ",
            $"svg {{ color: {cssHex}; }}",
            "svg * { fill: currentColor !important; stroke: currentColor !important; }",
            "svg [fill=\"none\"] { fill: none !important; }",
            "svg [stroke=\"none\"] { stroke: none !important; }"
        );
        
        if (Current != null)
        {
            Current.Resources["SvgCss"] = svgCss;
            Current.Resources["SvgOpacityValue"] = svgOpacity;
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainViewModel = Services.GetRequiredService<MainViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            desktop.Exit += async (_, _) =>
            {
                if (Services.GetService(typeof(ElevatedHelperController)) is ElevatedHelperController helperController)
                {
                    await helperController.DisposeAsync();
                }
            };

            desktop.MainWindow.Loaded += (sender, args) =>
            {
                var editSessionManager = new EditSessionManager(mainViewModel);
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
