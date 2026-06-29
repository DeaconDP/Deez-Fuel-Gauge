using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class DirectBillingServiceTests
{
    [Fact]
    public async Task EnrichAsync_returns_unavailable_pro_limits_when_disabled()
    {
        var settings = new WidgetSettings
        {
            OpenAi = new ProviderBillingSettings { ShowProLimits = false },
            Claude = new ProviderBillingSettings { ShowProLimits = false },
            Gemini = new ProviderBillingSettings { ShowProLimits = false },
            OpenRouter = new ProviderBillingSettings { ShowProLimits = false },
            OpenCode = new ProviderBillingSettings { ShowProLimits = false, ShowDirectSource = false }
        };

        var source = new UsageSnapshot { PercentUsed = 42 };
        using var service = new DirectBillingService();
        var enriched = await service.EnrichAsync(source, settings);

        Assert.False(enriched.Codex.IsAvailable);
        Assert.False(enriched.ClaudePro.IsAvailable);
        Assert.False(enriched.Antigravity.IsAvailable);
        Assert.False(enriched.OpenRouter.IsAvailable);
        Assert.False(enriched.OpenCode.IsAvailable);
        Assert.Equal(42, enriched.PercentUsed);
    }

    [Fact]
    public async Task EnrichAsync_fetches_enabled_providers_in_parallel()
    {
        var settings = new WidgetSettings
        {
            OpenAi = new ProviderBillingSettings { ShowProLimits = true },
            Claude = new ProviderBillingSettings { ShowProLimits = true },
            Gemini = new ProviderBillingSettings { ShowProLimits = true },
            OpenRouter = new ProviderBillingSettings { ShowProLimits = true },
            OpenCode = new ProviderBillingSettings { ShowProLimits = true, ShowDirectSource = true }
        };

        var source = new UsageSnapshot { PercentUsed = 10 };
        using var service = new DirectBillingService();
        var enriched = await service.EnrichAsync(source, settings);

        Assert.Equal(10, enriched.PercentUsed);
        Assert.False(enriched.Codex.IsAvailable);
        Assert.False(enriched.ClaudePro.IsAvailable);
    }
}
