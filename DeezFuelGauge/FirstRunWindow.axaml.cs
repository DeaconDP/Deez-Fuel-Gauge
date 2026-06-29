using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace DeezFuelGauge;

public partial class FirstRunWindow : Window
{
    public bool OpenSettingsRequested { get; private set; }

    public FirstRunWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OpenSettings_Click(object? sender, RoutedEventArgs e)
    {
        OpenSettingsRequested = true;
        Close();
    }

    private void Dismiss_Click(object? sender, RoutedEventArgs e) => Close();
}
