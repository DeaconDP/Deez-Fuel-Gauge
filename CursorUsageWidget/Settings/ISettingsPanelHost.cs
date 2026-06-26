namespace CursorUsageWidget.Settings;

public interface ISettingsPanelHost
{
    void OnSettingsChanged();

    void OnSettingsLayoutChanged();

    Task OnEasySetupCompletedAsync();
}
