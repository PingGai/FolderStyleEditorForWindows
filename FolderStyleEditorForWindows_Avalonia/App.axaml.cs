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