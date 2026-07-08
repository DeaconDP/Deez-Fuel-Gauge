using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DeezFuelGauge.Models;

namespace DeezFuelGauge.Settings;

public partial class SettingsPanel : UserControl
{
    private WidgetSettings? _settings;

    public SettingsPanelViewModel ViewModel => (SettingsPanelViewModel)DataContext!;

    public SettingsPanel()
    {
        InitializeComponent();
    }

    public void Initialize(SettingsPanelViewModel viewModel, WidgetSettings settings)
    {
        _settings = settings;
        DataContext = viewModel;
        viewModel.Load(settings);
    }

    public void ReloadFromSettings(WidgetSettings settings)
    {
        _settings = settings;
        ViewModel.Load(settings);
    }

    public void CommitToSettings(WidgetSettings settings) => ViewModel.Commit(settings);

    internal void NotifyLayoutChanged() => ViewModel.NotifyLayoutChanged();

    internal void RequestConnect(ProviderSourceKind kind) =>
        _ = ConnectAsync(kind);

    internal void RequestTest(ProviderSourceKind kind) =>
        _ = TestAsync(kind);

    internal void RequestSaveApiKey(ProviderSourceKind kind, string? text) =>
        ViewModel.SaveApiKey(kind, text);

    internal void RequestSaveManagementApiKey(ProviderSourceKind kind, string? text) =>
        ViewModel.SaveManagementApiKey(kind, text);

    internal void RequestSaveSession(ProviderSourceKind kind, string? text) =>
        ViewModel.SaveSession(kind, text);

    internal void RequestClearApiKey(ProviderSourceKind kind) =>
        ViewModel.ClearApiKey(kind);

    internal void RequestClearManagementApiKey(ProviderSourceKind kind) =>
        ViewModel.ClearManagementApiKey(kind);

    internal void RequestClearSession(ProviderSourceKind kind) =>
        ViewModel.ClearSession(kind);

    internal void RequestBeginClaudeSignIn(ProviderSourceKind kind) =>
        BeginClaudeSignIn(kind);

    internal void RequestCompleteClaudeSignIn(ProviderSourceKind kind, string? code) =>
        _ = CompleteClaudeSignInAsync(kind, code);

    internal void RequestDisconnectClaudeOAuth(ProviderSourceKind kind) =>
        ViewModel.DisconnectClaudeOAuth(RequireSettings());

    private WidgetSettings RequireSettings() =>
        _settings ?? throw new InvalidOperationException("SettingsPanel is not initialized.");

    private async Task ConnectAsync(ProviderSourceKind kind)
    {
        await ViewModel.ConnectAsync(kind, RequireSettings());
    }

    private async Task TestAsync(ProviderSourceKind kind)
    {
        await ViewModel.TestAsync(kind, RequireSettings());
    }

    private void BeginClaudeSignIn(ProviderSourceKind kind)
    {
        if (kind != ProviderSourceKind.ClaudePro)
            return;

        ViewModel.BeginClaudeSignIn(RequireSettings());
    }

    private async Task CompleteClaudeSignInAsync(ProviderSourceKind kind, string? code)
    {
        if (kind != ProviderSourceKind.ClaudePro)
            return;

        await ViewModel.CompleteClaudeSignInAsync(RequireSettings(), code);
    }

    private void SettingsPanel_PointerPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;

    private void SectionHeader_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { Tag: SettingsExpandedProvider provider })
            return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        ViewModel.ToggleExpandedProvider(provider);
        e.Handled = true;
    }

    private void MasterEnable_Changed(object? sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: ProviderSettingsSectionViewModel section, IsChecked: { } enabled })
            return;

        ViewModel.OnMasterEnableChanged(section, enabled);
    }
}
