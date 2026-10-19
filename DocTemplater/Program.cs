using Avalonia;
using Avalonia.ReactiveUI;
using System;
using DocTemplater.Services;
using Velopack;
using System.Runtime.InteropServices;

namespace DocTemplater;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
        ApplicationInstanceCoordinator? instance = null;
        try { instance = ApplicationInstanceCoordinator.Acquire(DataRoot); }
        catch (Exception ex)
        {
            if (OperatingSystem.IsWindows()) MessageBoxTimeout(IntPtr.Zero, ex.Message, "DocTemplater — запуск остановлен", 0x10, 0, 5000);
            Environment.ExitCode = 1;
            return;
        }
        try { BuildAvaloniaApp().StartWithClassicDesktopLifetime(args); }
        finally { instance?.Dispose(); }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxTimeoutW")]
    private static extern int MessageBoxTimeout(IntPtr owner, string text, string caption, uint type, ushort language, uint timeout);
    public static string DataRoot =>
#if UPDATE_FIXTURE
        Environment.GetEnvironmentVariable("DOCTEMPLATER_FIXTURE_DATA") ?? throw new InvalidOperationException("Fixture data root is required.");
#else
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DocTemplater");
#endif

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}
