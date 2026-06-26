using CursorUsageWidget.Models;

namespace CursorUsageWidget.Services;

public static class ProviderConnectionStateHelper
{
    public static string ToColor(ProviderConnectionState state) =>
        state switch
        {
            ProviderConnectionState.Connected => "#FF4CAF50",
            ProviderConnectionState.NeedsSetup => "#FFFFA726",
            _ => "#FF666666"
        };

    public static ProviderConnectionState Aggregate(params ProviderConnectionState[] states)
    {
        var active = states.Where(s => s != ProviderConnectionState.Off).ToArray();
        if (active.Length == 0)
            return ProviderConnectionState.Off;

        if (active.Any(s => s == ProviderConnectionState.NeedsSetup))
            return ProviderConnectionState.NeedsSetup;

        return ProviderConnectionState.Connected;
    }

    public static ProviderConnectionState FromConnected(bool enabled, bool connected) =>
        !enabled ? ProviderConnectionState.Off
        : connected ? ProviderConnectionState.Connected
        : ProviderConnectionState.NeedsSetup;
}
