namespace DeezFuelGauge.Settings;

public interface ISettingsPanelHost
{
    void OnSettingsChanged();

    void OnSettingsLayoutChanged();

    Task OnEasySetupCompletedAsync();
}
