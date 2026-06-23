using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using CursorUsageWidget.Models;
using CursorUsageWidget.Services;

namespace CursorUsageWidget;

public partial class MainWindow : Window
{
    private readonly UsageClient _usageClient = new();
    private readonly DispatcherTimer _pollTimer;
    private bool _isRefreshing;
    private double _lastPercentUsed;

    public MainWindow()
    {
        InitializeComponent();

        var settings = SettingsStore.Load();
        Left = settings.Left;
        Top = settings.Top;

        _pollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(5)
        };
        _pollTimer.Tick += async (_, _) => await RefreshAsync();
        _pollTimer.Start();

        Loaded += async (_, _) => await RefreshAsync();
        SizeChanged += (_, _) => UpdateProgressWidth(_lastPercentUsed);
        LocationChanged += (_, _) => SavePosition();
        Closing += (_, _) => SavePosition();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private async void RefreshMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private void QuitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void FooterLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private async Task RefreshAsync()
    {
        if (_isRefreshing)
            return;

        _isRefreshing = true;
        try
        {
            var tokens = CursorTokenReader.Read();
            _usageClient.SetTokens(tokens.AccessToken, tokens.RefreshToken);
            var snapshot = await _usageClient.FetchAsync();
            ApplySnapshot(snapshot);
        }
        catch
        {
            ApplySnapshot(UsageSnapshot.Error("Can't fetch usage"));
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void ApplySnapshot(UsageSnapshot snapshot)
    {
        if (snapshot.IsError)
        {
            PercentText.Text = snapshot.ErrorMessage ?? "Error";
            RemainingText.Text = "";
            ProgressFill.Width = 0;
            ProgressFill.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));
            PercentText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));
            return;
        }

        _lastPercentUsed = snapshot.PercentUsed;
        var percent = Math.Round(snapshot.PercentUsed);
        PercentText.Text = $"{percent}% used";
        RemainingText.Text = snapshot.RemainingLabel;
        UpdateProgressWidth(snapshot.PercentUsed);

        var accent = GetAccentColor(snapshot.PercentUsed);
        ProgressFill.Background = new SolidColorBrush(accent);
        PercentText.Foreground = new SolidColorBrush(accent);
    }

    private void UpdateProgressWidth(double percentUsed)
    {
        var trackWidth = ProgressTrack.ActualWidth;
        if (trackWidth <= 0)
            return;

        ProgressFill.Width = trackWidth * (percentUsed / 100.0);
    }

    private static Color GetAccentColor(double percentUsed)
    {
        if (percentUsed >= 90)
            return Color.FromRgb(0xF4, 0x43, 0x36);
        if (percentUsed >= 70)
            return Color.FromRgb(0xFF, 0x98, 0x00);
        return Color.FromRgb(0x4C, 0xAF, 0x50);
    }

    private void SavePosition()
    {
        SettingsStore.Save(new WidgetSettings
        {
            Left = Left,
            Top = Top
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        _pollTimer.Stop();
        _usageClient.Dispose();
        base.OnClosed(e);
    }
}
