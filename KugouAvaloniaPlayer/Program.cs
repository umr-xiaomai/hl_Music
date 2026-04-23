using System;
using System.Runtime.Versioning;
using System.Threading;
using Avalonia;
using Velopack;
using Velopack.Locators;
using Velopack.Windows;

namespace KugouAvaloniaPlayer;

internal sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    private static Mutex? _mutex;

    [STAThread]
    public static void Main(string[] args)
    {
        var velopack = VelopackApp.Build();
        if (OperatingSystem.IsWindows())
        {
#pragma warning disable CA1416
            velopack
                .OnAfterInstallFastCallback(_ => RefreshWindowsShortcuts())
                .OnAfterUpdateFastCallback(_ => RefreshWindowsShortcuts());
#pragma warning restore CA1416
        }

        velopack.Run();
        _mutex = new Mutex(true, "KugouAvaloniaPlayer", out var createdNew);
        if (!createdNew) return;
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    [SupportedOSPlatform("windows")]
    private static void RefreshWindowsShortcuts()
    {
        if (!VelopackLocator.IsCurrentSet) return;

#pragma warning disable CS0618
        var shortcuts = new Shortcuts(VelopackLocator.Current);
        var locations = ShortcutLocation.Desktop | ShortcutLocation.StartMenu;
        shortcuts.CreateShortcutForThisExe(locations);
#pragma warning restore CS0618
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
