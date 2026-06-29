using System.Text.Json;
using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

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
            Claude = new ProviderBillingSettings { IsVisible = true, ShowDetails = false },
            ShowBreakdown = false
        };

        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<WidgetSettings>(json)!;

        Assert.False(restored.OpenAi.IsVisible);
        Assert.False(restored.OpenAi.ShowDetails);
        Assert.False(restored.Claude.ShowDetails);
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
    public void RoundTrip_preserves_claude_pro_and_api_console_fields()
    {
        var settings = new WidgetSettings
        {
            Claude = new ProviderBillingSettings
            {
                ShowProLimits = true,
                ShowApiConsoleBilling = true,
                ProSessionCredentialId = "claude-pro-abc",
                MonthlyBudgetUsd = 100,
                CredentialId = "claude-admin-xyz"
            }
        };

        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<WidgetSettings>(json)!;

        Assert.True(restored.Claude.ShowProLimits);
        Assert.True(restored.Claude.ShowApiConsoleBilling);
        Assert.Equal("claude-pro-abc", restored.Claude.ProSessionCredentialId);
        Assert.Equal("claude-admin-xyz", restored.Claude.CredentialId);
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
            Claude = new ProviderBillingSettings
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
        Assert.False(restored.Claude.ShowDetails);
        Assert.True(restored.Claude.ShowDirectDetails);
        Assert.True(restored.Claude.ShowProDetails);
    }

    [Fact]
    public void Deserialize_inherits_missing_per_source_details_from_show_details()
    {
        var json = """{"OpenAi":{"ShowDetails":false},"Claude":{"ShowDetails":true}}""";

        var settings = JsonSerializer.Deserialize<WidgetSettings>(json)!;

        Assert.False(settings.OpenAi.EffectiveShowDirectDetails);
        Assert.False(settings.OpenAi.EffectiveShowProDetails);
        Assert.True(settings.Claude.EffectiveShowDirectDetails);
        Assert.True(settings.Claude.EffectiveShowProDetails);
    }

    [Fact]
    public void RoundTrip_preserves_limits_expanded_state()
    {
        var settings = new WidgetSettings
        {
            IsCodexLimitsExpanded = true,
            IsClaudeProLimitsExpanded = false,
            IsAntigravityLimitsExpanded = true
        };

        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<WidgetSettings>(json)!;

        Assert.True(restored.IsCodexLimitsExpanded);
        Assert.False(restored.IsClaudeProLimitsExpanded);
        Assert.True(restored.IsAntigravityLimitsExpanded);
    }

    [Fact]
    public void RoundTrip_preserves_provider_expanded_state()
    {
        var settings = new WidgetSettings
        {
            IsCursorProviderExpanded = true,
            IsOpenAiProviderExpanded = false,
            IsClaudeProviderExpanded = true,
            IsGeminiProviderExpanded = false
        };

        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<WidgetSettings>(json)!;

        Assert.True(restored.IsCursorProviderExpanded);
        Assert.False(restored.IsOpenAiProviderExpanded);
        Assert.True(restored.IsClaudeProviderExpanded);
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
            Claude = new ProviderBillingSettings { ShowProBreakdown = true },
            Gemini = new ProviderBillingSettings { ShowProBreakdown = false }
        };

        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<WidgetSettings>(json)!;

        Assert.False(restored.OpenAi.ShowProBreakdown);
        Assert.True(restored.Claude.ShowProBreakdown);
        Assert.False(restored.Gemini.ShowProBreakdown);
    }

    [Fact]
    public void Deserialize_defaults_show_pro_breakdown_to_true_when_missing()
    {
        var json = """{"OpenAi":{},"Claude":{},"Gemini":{}}""";

        var settings = JsonSerializer.Deserialize<WidgetSettings>(json)!;

        Assert.True(settings.OpenAi.ShowProBreakdown);
        Assert.True(settings.Claude.ShowProBreakdown);
        Assert.True(settings.Gemini.ShowProBreakdown);
    }

    [Fact]
    public void MigrateClaudeSettings_moves_legacy_show_direct_source_to_api_console()
    {
        var settings = new WidgetSettings
        {
            Claude = new ProviderBillingSettings { ShowDirectSource = true }
        };

        SettingsStore.MigrateClaudeSettings(settings);

        Assert.False(settings.Claude.ShowDirectSource);
        Assert.True(settings.Claude.ShowApiConsoleBilling);
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

    [Fact]
    public void RoundTrip_preserves_pinned_position()
    {
        var settings = new WidgetSettings
        {
            IsPositionPinned = true,
            Left = 420,
            Top = 180
        };

        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<WidgetSettings>(json)!;

        Assert.True(restored.IsPositionPinned);
        Assert.Equal(420, restored.Left);
        Assert.Equal(180, restored.Top);
    }

    [Fact]
    public void Deserialize_defaults_is_position_pinned_to_false_when_missing()
    {
        var json = """{"Left":10,"Top":20}""";

        var settings = JsonSerializer.Deserialize<WidgetSettings>(json)!;

        Assert.False(settings.IsPositionPinned);
    }
}
