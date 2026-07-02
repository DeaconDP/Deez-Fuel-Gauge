using System.Text.Json;
using CursorUsageWidget.Models;
using CursorUsageWidget.Services;
using Xunit;

namespace CursorUsageWidget.Tests;

public sealed class WidgetSettingsTests
{
    [Fact]
    public void Deserialize_uses_visible_defaults_for_legacy_settings()
    {
        var json = """{"Left":10,"Top":20,"IsBreakdownExpanded":true}""";

        var settings = JsonSerializer.Deserialize<WidgetSettings>(json)!;

        Assert.Equal(10, settings.Left);
        Assert.True(settings.Cursor.IsVisible);
        Assert.True(settings.OpenAi.IsVisible);
        Assert.True(settings.ShowBreakdown);
    }

    [Fact]
    public void RoundTrip_preserves_provider_display_options()
    {
        var settings = new WidgetSettings
        {
            OpenAi = new ProviderBillingSettings { IsVisible = false, ShowDetails = false },
            Gemini = new ProviderBillingSettings { IsVisible = true, ShowDetails = false },
            ShowBreakdown = false
        };

        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<WidgetSettings>(json)!;

        Assert.False(restored.OpenAi.IsVisible);
        Assert.False(restored.OpenAi.ShowDetails);
        Assert.False(restored.Gemini.ShowDetails);
        Assert.False(restored.ShowBreakdown);
    }

    [Fact]
    public void RoundTrip_preserves_direct_billing_fields()
    {
        var settings = new WidgetSettings
        {
            OpenAi = new ProviderBillingSettings
            {
                ShowDirectSource = true,
                MonthlyBudgetUsd = 250,
                OrganizationId = "org-123",
                CredentialId = "openai-abc"
            },
            Gemini = new ProviderBillingSettings
            {
                ShowDirectSource = true,
                ProjectId = "my-project",
                MonthlyBudgetUsd = 50
            }
        };

        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<WidgetSettings>(json)!;

        Assert.True(restored.OpenAi.ShowDirectSource);
        Assert.Equal(250, restored.OpenAi.MonthlyBudgetUsd);
        Assert.Equal("org-123", restored.OpenAi.OrganizationId);
        Assert.Equal("my-project", restored.Gemini.ProjectId);
    }

    [Fact]
    public void RoundTrip_preserves_per_source_detail_options()
    {
        var settings = new WidgetSettings
        {
            OpenAi = new ProviderBillingSettings
            {
                ShowDetails = true,
                ShowDirectDetails = false,
                ShowProDetails = false
            },
            Gemini = new ProviderBillingSettings
            {
                ShowDetails = false,
                ShowDirectDetails = true,
                ShowProDetails = true
            }
        };

        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<WidgetSettings>(json)!;

        Assert.True(restored.OpenAi.ShowDetails);
        Assert.False(restored.OpenAi.ShowDirectDetails);
        Assert.False(restored.OpenAi.ShowProDetails);
        Assert.False(restored.OpenAi.EffectiveShowDirectDetails);
        Assert.False(restored.OpenAi.EffectiveShowProDetails);
        Assert.False(restored.Gemini.ShowDetails);
        Assert.True(restored.Gemini.ShowDirectDetails);
        Assert.True(restored.Gemini.ShowProDetails);
    }

    [Fact]
    public void Deserialize_inherits_missing_per_source_details_from_show_details()
    {
        var json = """{"OpenAi":{"ShowDetails":false},"Gemini":{"ShowDetails":true}}""";

        var settings = JsonSerializer.Deserialize<WidgetSettings>(json)!;

        Assert.False(settings.OpenAi.EffectiveShowDirectDetails);
        Assert.False(settings.OpenAi.EffectiveShowProDetails);
        Assert.True(settings.Gemini.EffectiveShowDirectDetails);
        Assert.True(settings.Gemini.EffectiveShowProDetails);
    }

    [Fact]
    public void RoundTrip_preserves_limits_expanded_state()
    {
        var settings = new WidgetSettings
        {
            IsCodexLimitsExpanded = true,
            IsAntigravityLimitsExpanded = true
        };

        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<WidgetSettings>(json)!;

        Assert.True(restored.IsCodexLimitsExpanded);
        Assert.True(restored.IsAntigravityLimitsExpanded);
    }

    [Fact]
    public void RoundTrip_preserves_provider_expanded_state()
    {
        var settings = new WidgetSettings
        {
            IsCursorProviderExpanded = true,
            IsOpenAiProviderExpanded = false,
            IsGeminiProviderExpanded = false
        };

        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<WidgetSettings>(json)!;

        Assert.True(restored.IsCursorProviderExpanded);
        Assert.False(restored.IsOpenAiProviderExpanded);
        Assert.False(restored.IsGeminiProviderExpanded);
    }

    [Fact]
    public void RoundTrip_preserves_settings_expanded_provider()
    {
        var settings = new WidgetSettings
        {
            SettingsExpandedProvider = SettingsExpandedProvider.OpenAi
        };

        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<WidgetSettings>(json)!;

        Assert.Equal(SettingsExpandedProvider.OpenAi, restored.SettingsExpandedProvider);
    }

    [Fact]
    public void Deserialize_defaults_settings_expanded_provider_to_none()
    {
        var json = """{"Left":10,"Top":20}""";

        var settings = JsonSerializer.Deserialize<WidgetSettings>(json)!;

        Assert.Equal(SettingsExpandedProvider.None, settings.SettingsExpandedProvider);
    }

    [Fact]
    public void RoundTrip_preserves_show_pro_breakdown()
    {
        var settings = new WidgetSettings
        {
            OpenAi = new ProviderBillingSettings { ShowProBreakdown = false },
            Gemini = new ProviderBillingSettings { ShowProBreakdown = false }
        };

        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<WidgetSettings>(json)!;

        Assert.False(restored.OpenAi.ShowProBreakdown);
        Assert.False(restored.Gemini.ShowProBreakdown);
    }

    [Fact]
    public void Deserialize_defaults_show_pro_breakdown_to_true_when_missing()
    {
        var json = """{"OpenAi":{},"Gemini":{}}""";

        var settings = JsonSerializer.Deserialize<WidgetSettings>(json)!;

        Assert.True(settings.OpenAi.ShowProBreakdown);
        Assert.True(settings.Gemini.ShowProBreakdown);
    }

    [Fact]
    public void MigrateGeminiSettings_moves_legacy_show_direct_source_to_antigravity_limits()
    {
        var settings = new WidgetSettings
        {
            Gemini = new ProviderBillingSettings { ShowDirectSource = true }
        };

        SettingsStore.MigrateGeminiSettings(settings);

        Assert.False(settings.Gemini.ShowDirectSource);
        Assert.True(settings.Gemini.ShowProLimits);
    }
}
