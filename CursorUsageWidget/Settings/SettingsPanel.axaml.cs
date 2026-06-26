using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CursorUsageWidget.Models;

namespace CursorUsageWidget.Settings;

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

    private void SettingsPanel_PointerPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;

    private WidgetSettings RequireSettings() =>
        _settings ?? throw new InvalidOperationException("SettingsPanel is not initialized.");

    private void ToggleHeader(SettingsExpandedProvider provider, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        ViewModel.ToggleExpandedProvider(provider);
        e.Handled = true;
    }

    private void CursorHeader_PointerPressed(object? sender, PointerPressedEventArgs e) =>
        ToggleHeader(SettingsExpandedProvider.Cursor, e);

    private void OpenAiHeader_PointerPressed(object? sender, PointerPressedEventArgs e) =>
        ToggleHeader(SettingsExpandedProvider.OpenAi, e);

    private void ClaudeHeader_PointerPressed(object? sender, PointerPressedEventArgs e) =>
        ToggleHeader(SettingsExpandedProvider.Claude, e);

    private void GeminiHeader_PointerPressed(object? sender, PointerPressedEventArgs e) =>
        ToggleHeader(SettingsExpandedProvider.Gemini, e);

    private void OpenRouterHeader_PointerPressed(object? sender, PointerPressedEventArgs e) =>
        ToggleHeader(SettingsExpandedProvider.OpenRouter, e);

    private void OpenCodeHeader_PointerPressed(object? sender, PointerPressedEventArgs e) =>
        ToggleHeader(SettingsExpandedProvider.OpenCode, e);

    private void DiskHeader_PointerPressed(object? sender, PointerPressedEventArgs e) =>
        ToggleHeader(SettingsExpandedProvider.Disk, e);

    private void ClearTextBox(TextBox box)
    {
        box.Text = "";
    }

    private void OpenAiApiKeyBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box)
            return;

        ViewModel.SaveOpenAiApiKey(box.Text);
        ClearTextBox(box);
    }

    private void OpenAiSessionCookieBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box)
            return;

        ViewModel.SaveOpenAiSessionCookie(box.Text);
        ClearTextBox(box);
    }

    private void ClaudeApiKeyBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box)
            return;

        ViewModel.SaveClaudeApiKey(box.Text);
        ClearTextBox(box);
    }

    private void ClearOpenAiApiKey_Click(object? sender, RoutedEventArgs e) => ViewModel.ClearOpenAiApiKey();

    private void ClearOpenAiSessionCookie_Click(object? sender, RoutedEventArgs e) => ViewModel.ClearOpenAiSessionCookie();

    private void ClearClaudeApiKey_Click(object? sender, RoutedEventArgs e) => ViewModel.ClearClaudeApiKey();

    private void OpenRouterApiKeyBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box)
            return;

        ViewModel.SaveOpenRouterApiKey(box.Text);
        ClearTextBox(box);
    }

    private void ClearOpenRouterApiKey_Click(object? sender, RoutedEventArgs e) => ViewModel.ClearOpenRouterApiKey();

    private void OpenCodeSessionCookieBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box)
            return;

        ViewModel.SaveOpenCodeSessionCookie(box.Text);
        ClearTextBox(box);
    }

    private void ClearOpenCodeSessionCookie_Click(object? sender, RoutedEventArgs e) => ViewModel.ClearOpenCodeSessionCookie();

    private void OpenClaudeAi_Click(object? sender, RoutedEventArgs e) => ViewModel.OpenClaudeAi();

    private void OpenOpenCode_Click(object? sender, RoutedEventArgs e) => ViewModel.OpenOpenCode();

    private async void EasySetupCursor_Click(object? sender, RoutedEventArgs e) =>
        await ViewModel.RunEasySetupCursorAsync(RequireSettings());

    private async void EasySetupOpenAi_Click(object? sender, RoutedEventArgs e) =>
        await ViewModel.RunEasySetupOpenAiAsync(RequireSettings());

    private async void RefreshClaudePro_Click(object? sender, RoutedEventArgs e) =>
        await ViewModel.RefreshClaudeProAsync(RequireSettings());

    private async void EasySetupGemini_Click(object? sender, RoutedEventArgs e) =>
        await ViewModel.RunEasySetupGeminiAsync(RequireSettings());

    private void EasySetupDisk_Click(object? sender, RoutedEventArgs e) =>
        ViewModel.RunEasySetupDisk(RequireSettings());

    private async void TestOpenAi_Click(object? sender, RoutedEventArgs e) =>
        await ViewModel.TestOpenAiAsync(RequireSettings());

    private async void TestCodex_Click(object? sender, RoutedEventArgs e) =>
        await ViewModel.TestCodexAsync(RequireSettings());

    private async void TestClaudeApiConsole_Click(object? sender, RoutedEventArgs e) =>
        await ViewModel.TestClaudeApiConsoleAsync(RequireSettings());

    private async void TestAntigravity_Click(object? sender, RoutedEventArgs e) =>
        await ViewModel.TestAntigravityAsync(RequireSettings());

    private async void TestOpenRouter_Click(object? sender, RoutedEventArgs e) =>
        await ViewModel.TestOpenRouterAsync(RequireSettings());

    private async void TestOpenCode_Click(object? sender, RoutedEventArgs e) =>
        await ViewModel.TestOpenCodeAsync(RequireSettings());

    private async void EasySetupOpenRouter_Click(object? sender, RoutedEventArgs e) =>
        await ViewModel.RunEasySetupOpenRouterAsync(RequireSettings());
}
