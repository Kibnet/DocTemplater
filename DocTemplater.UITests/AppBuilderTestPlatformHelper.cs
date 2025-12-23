using Avalonia;
using Avalonia.Headless;
using System;

namespace DocTemplater.UITests;

public class AppBuilderTestPlatformHelper
{
    public static AppBuilder BuildAvaloniaApp() => 
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = true
            });
}