using Avalonia;
using Avalonia.Svg.Skia;
using Avalonia.Win32;
using FolderStyleEditorForWindows.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FolderStyleEditorForWindows;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains("--elevated-helper", StringComparer.OrdinalIgnoreCase))
        {
            RunElevatedHelper(args).GetAwaiter().GetResult();
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        GC.KeepAlive(typeof(SvgImageExtension).Assembly);
        GC.KeepAlive(typeof(Avalonia.Svg.Skia.Svg).Assembly);

        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .With(new Win32PlatformOptions
            {
                CompositionMode = new[]
                {
                    Win32CompositionMode.WinUIComposition,
                    Win32CompositionMode.DirectComposition,
                    Win32CompositionMode.RedirectionSurface
                },
                RenderingMode = new[]
                {
                    Win32RenderingMode.AngleEgl,
                    Win32RenderingMode.Software
                }
            })
            .LogToTrace();
    }

    private static Task<int> RunElevatedHelper(string[] args)
    {
        string? pipeName = null;
        string? sessionToken = null;
        var parentPid = 0;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--pipe-name" when i + 1 < args.Length:
                    pipeName = args[++i];
                    break;
                case "--session-token" when i + 1 < args.Length:
                    sessionToken = args[++i];
                    break;
                case "--parent-pid" when i + 1 < args.Length:
                    int.TryParse(args[++i], out parentPid);
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(pipeName) || string.IsNullOrWhiteSpace(sessionToken) || parentPid <= 0)
        {
            return Task.FromResult(1);
        }

        return ElevatedHelperRunner.RunAsync(pipeName, sessionToken, parentPid);
    }
}
