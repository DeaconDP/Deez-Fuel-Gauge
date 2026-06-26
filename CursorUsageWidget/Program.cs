using Avalonia;
using Avalonia.Native;

namespace CursorUsageWidget;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .With(new AvaloniaNativePlatformOptions { OverlayPopups = true })
            .LogToTrace();
}
