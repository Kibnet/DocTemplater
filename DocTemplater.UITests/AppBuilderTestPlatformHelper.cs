using Avalonia;
using Avalonia.Headless;
using Avalonia.ReactiveUI;
using System;

namespace DocTemplater.UITests;

public class AppBuilderTestPlatformHelper
{
    public static AppBuilder BuildAvaloniaApp() => 
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false
            })
            .UseReactiveUI();
}
