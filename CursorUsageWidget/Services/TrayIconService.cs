using Avalonia.Controls;

namespace CursorUsageWidget.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly TrayIcon _trayIcon;

    public TrayIconService(Action onRefresh, Action onQuit)
    {
        var menu = new NativeMenu();
        var refreshItem = new NativeMenuItem("Refresh");
        refreshItem.Click += (_, _) => onRefresh();
        menu.Items.Add(refreshItem);
        var quitItem = new NativeMenuItem("Quit");
        quitItem.Click += (_, _) => onQuit();
        menu.Items.Add(quitItem);

        _trayIcon = new TrayIcon
        {
            ToolTipText = "Cursor Usage",
            IsVisible = true,
            Menu = menu
        };

        TrySetIcon();
    }

    private void TrySetIcon()
    {
        try
        {
            using var stream = Avalonia.Platform.AssetLoader.Open(new Uri("avares://CursorUsageWidget/Assets/app-icon.png"));
            _trayIcon.Icon = new WindowIcon(stream);
        }
        catch
        {
            // icon optional
        }
    }

    public void Dispose() => _trayIcon.Dispose();
}
