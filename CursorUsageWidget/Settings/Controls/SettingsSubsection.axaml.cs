using Avalonia;
using Avalonia.Controls;

namespace CursorUsageWidget.Settings.Controls;

public partial class SettingsSubsection : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<SettingsSubsection, string>(nameof(Title));

    public static readonly StyledProperty<string?> StatusProperty =
        AvaloniaProperty.Register<SettingsSubsection, string?>(nameof(Status));

    public static readonly StyledProperty<string?> HelpTextProperty =
        AvaloniaProperty.Register<SettingsSubsection, string?>(nameof(HelpText));

    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<SettingsSubsection, object?>(nameof(Actions));

    public static readonly StyledProperty<object?> BodyProperty =
        AvaloniaProperty.Register<SettingsSubsection, object?>(nameof(Body));

    public static readonly StyledProperty<bool> HasStatusProperty =
        AvaloniaProperty.Register<SettingsSubsection, bool>(nameof(HasStatus));

    public static readonly StyledProperty<bool> HasHelpTextProperty =
        AvaloniaProperty.Register<SettingsSubsection, bool>(nameof(HasHelpText));

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public string? HelpText
    {
        get => GetValue(HelpTextProperty);
        set => SetValue(HelpTextProperty, value);
    }

    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    public object? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    public bool HasStatus
    {
        get => GetValue(HasStatusProperty);
        private set => SetValue(HasStatusProperty, value);
    }

    public bool HasHelpText
    {
        get => GetValue(HasHelpTextProperty);
        private set => SetValue(HasHelpTextProperty, value);
    }

    public SettingsSubsection() => InitializeComponent();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == StatusProperty)
            HasStatus = !string.IsNullOrWhiteSpace(change.GetNewValue<string?>());

        if (change.Property == HelpTextProperty)
            HasHelpText = !string.IsNullOrWhiteSpace(change.GetNewValue<string?>());
    }
}
