using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace CursorUsageWidget.Settings.Controls;

public partial class SettingsSourceRow : UserControl
{
    public SettingsSourceRow()
    {
        InitializeComponent();
    }

    private ProviderSourceViewModel? Vm => DataContext as ProviderSourceViewModel;

    private SettingsPanel? FindPanel() =>
        this.GetVisualAncestors().OfType<SettingsPanel>().FirstOrDefault();

    private void Connect_Click(object? sender, RoutedEventArgs e)
    {
        if (Vm is not null)
            FindPanel()?.RequestConnect(Vm.Kind);
    }

    private void Test_Click(object? sender, RoutedEventArgs e)
    {
        if (Vm is not null)
            FindPanel()?.RequestTest(Vm.Kind);
    }

    private void AdvancedToggle_Click(object? sender, RoutedEventArgs e)
    {
        if (Vm is not null)
            Vm.IsAdvancedExpanded = !Vm.IsAdvancedExpanded;
    }

    private void ApiKeyBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (Vm is null || sender is not TextBox box)
            return;

        FindPanel()?.RequestSaveApiKey(Vm.Kind, box.Text);
        box.Text = "";
    }

    private void ManagementApiKeyBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (Vm is null || sender is not TextBox box)
            return;

        FindPanel()?.RequestSaveManagementApiKey(Vm.Kind, box.Text);
        box.Text = "";
    }

    private void SessionBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (Vm is null || sender is not TextBox box)
            return;

        FindPanel()?.RequestSaveSession(Vm.Kind, box.Text);
        box.Text = "";
    }

    private void ClearApiKey_Click(object? sender, RoutedEventArgs e)
    {
        if (Vm is not null)
            FindPanel()?.RequestClearApiKey(Vm.Kind);
    }

    private void ClearManagementApiKey_Click(object? sender, RoutedEventArgs e)
    {
        if (Vm is not null)
            FindPanel()?.RequestClearManagementApiKey(Vm.Kind);
    }

    private void ClearSession_Click(object? sender, RoutedEventArgs e)
    {
        if (Vm is not null)
            FindPanel()?.RequestClearSession(Vm.Kind);
    }
}
