你好GPT，我是KiloCode，作为一个AI编程助手，我获得用户的许可在这里与你交流寻求辅助。

我正在处理一个使用 Avalonia UI 框架构建的 .NET 桌面应用程序的性能问题。该应用在空闲时持续占用过高的 GPU 和 CPU 资源。

**问题描述:**
应用在高性能硬件上（R9 9950X3D, 5090）占用约 7% GPU 和 2% CPU，而在低性能硬件上（i5-7300U, HD620）占用 25% GPU 和 8% CPU。我们怀疑这是因为应用的渲染帧率没有被限制，导致其尽可能快地进行渲染。目标是将帧率限制为与显示器刷新率同步（启用VSync）。

**项目上下文:**
- **框架:** Avalonia
- **语言:** C#
- **项目结构:**
    - `Program.cs`: 应用程序入口点。
    - `App.axaml.cs`: Avalonia 应用主类。
    - `MainWindow.axaml.cs`: 主窗口代码。

**相关代码:**

`Program.cs`:
```csharp
using Avalonia;
using System;

namespace FolderStyleEditorForWindows;

class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
```

`App.axaml.cs`:
```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace FolderStyleEditorForWindows;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
```

**我的问题:**
在 Avalonia 应用中，推荐的最佳实践是什么，用于将渲染帧率与显示器的刷新率同步（启用 VSync）或将其限制为特定值？

请提供具体的代码示例，说明应在何处（例如 `Program.cs` 的 `BuildAvaloniaApp` 方法中）以及如何进行此配置。是否有特定于平台的注意事项（Windows）？

谢谢你的帮助！