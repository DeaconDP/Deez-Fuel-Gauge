using CursorUsageWidget.Models;
using CursorUsageWidget.Services;
using Xunit;

namespace CursorUsageWidget.Tests;

public sealed class DirectBillingServiceTests
{
    [Fact]
    public async Task EnrichAsync_returns_unavailable_pro_limits_when_disabled()
    {
        var settings = new WidgetSettings
        {
            OpenAi = new ProviderBillingSettings { ShowProLimits = false },
            Gemini = new ProviderBillingSettings { ShowProLimits = false },
            OpenRouter = new ProviderBillingSettings { ShowProLimits = false },
            OpenCode = new ProviderBillingSettings { ShowProLimits = false, ShowDirectSource = false }
        };

        var source = new UsageSnapshot { PercentUsed = 42 };
        using var service = new DirectBillingService();
        var enriched = await service.EnrichAsync(source, settings);

        Assert.False(enriched.Codex.IsAvailable);
        Assert.False(enriched.Antigravity.IsAvailable);
        Assert.False(enriched.OpenRouter.IsAvailable);
        Assert.False(enriched.OpenCode.IsAvailable);
        Assert.Equal(42, enriched.PercentUsed);
    }
}
