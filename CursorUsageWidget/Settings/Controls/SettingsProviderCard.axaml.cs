using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace CursorUsageWidget.Settings.Controls;

public partial class SettingsProviderCard : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<SettingsProviderCard, string>(nameof(Title));

    public static readonly StyledProperty<string> HeaderColorProperty =
        AvaloniaProperty.Register<SettingsProviderCard, string>(nameof(HeaderColor), "#FF888888");

    public static readonly StyledProperty<string> ChevronProperty =
        AvaloniaProperty.Register<SettingsProviderCard, string>(nameof(Chevron), "▾");

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<SettingsProviderCard, bool>(nameof(IsExpanded));

    public static readonly StyledProperty<object?> BodyProperty =
        AvaloniaProperty.Register<SettingsProviderCard, object?>(nameof(Body));

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string HeaderColor
    {
        get => GetValue(HeaderColorProperty);
        set => SetValue(HeaderColorProperty, value);
    }

    public string Chevron
    {
        get => GetValue(ChevronProperty);
        set => SetValue(ChevronProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public object? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    public event EventHandler<PointerPressedEventArgs>? HeaderPressed;

    public SettingsProviderCard() => InitializeComponent();

    private void Header_PointerPressed(object? sender, PointerPressedEventArgs e) =>
        HeaderPressed?.Invoke(this, e);
}
