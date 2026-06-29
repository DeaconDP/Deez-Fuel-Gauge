using Avalonia;
using Avalonia.Controls;

namespace DeezFuelGauge.Settings.Controls;

public partial class SettingsGroupHeader : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<SettingsGroupHeader, string>(nameof(Title));

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public SettingsGroupHeader() => InitializeComponent();
}
