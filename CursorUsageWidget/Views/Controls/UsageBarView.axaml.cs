using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using CursorUsageWidget.Services;

namespace CursorUsageWidget.Views.Controls;

public partial class UsageBarView : UserControl
{
    private readonly Grid _track;
    private readonly Border _fill;
    private double _lastPercent;

    public UsageBarView()
    {
        InitializeComponent();
        _track = this.FindControl<Grid>("Track")!;
        _fill = this.FindControl<Border>("Fill")!;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public void SetPercent(double percent)
    {
        _lastPercent = percent;
        _track.Opacity = 1;
        _fill.Background = new SolidColorBrush(UsageBarColors.GetColorForPercent(percent));
        ProviderBarPresenter.UpdateProgressWidth(_track, _fill, percent);
    }

    public void Reset()
    {
        _fill.Width = 0;
        _track.Opacity = 0.45;
        _lastPercent = 0;
    }
}
