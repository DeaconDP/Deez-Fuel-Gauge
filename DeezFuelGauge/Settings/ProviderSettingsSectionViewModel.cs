using System.Collections.ObjectModel;
using DeezFuelGauge.Models;
using DeezFuelGauge.Services;

namespace DeezFuelGauge.Settings;

public sealed class ProviderSettingsSectionViewModel : ViewModelBase
{
    private bool _masterEnable = true;
    private string _summaryStatus = "";
    private string _headerColor = ProviderConnectionStateHelper.ToColor(ProviderConnectionState.Off);

    public SettingsExpandedProvider ProviderId { get; init; }

    public string Title { get; init; } = "";

    public bool ShowMasterEnable { get; init; } = true;

    private bool _isExpanded;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
                OnPropertyChanged(nameof(Chevron));
        }
    }

    public ObservableCollection<ProviderSourceViewModel> Sources { get; } = [];

    public bool MasterEnable
    {
        get => _masterEnable;
        set => SetProperty(ref _masterEnable, value);
    }

    public string SummaryStatus
    {
        get => _summaryStatus;
        set => SetProperty(ref _summaryStatus, value);
    }

    public string HeaderColor
    {
        get => _headerColor;
        set => SetProperty(ref _headerColor, value);
    }

    public string Chevron => IsExpanded ? "▴" : "▾";
}
