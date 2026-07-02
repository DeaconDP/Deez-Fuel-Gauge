using System.Text.Json;
using CursorUsageWidget.Services;
using Xunit;

namespace CursorUsageWidget.Tests;

public sealed class AntigravityUsageClientTests
{
    internal const string SampleQuotaSummaryJson = """
        {
          "quota_groups": [
            {
              "display_name": "Gemini Models",
              "buckets": [
                {
                  "window": "weekly",
                  "remaining_fraction": 0.98,
                  "reset_time": "2026-06-23T02:43:38Z"
                },
                {
                  "window": "5h",
                  "remaining_fraction": 0.95,
                  "reset_time": "2026-06-16T07:43:38Z"
                }
              ]
            },
            {
              "display_name": "Claude and GPT models",
              "buckets": [
                {
                  "window": "weekly",
                  "remaining_fraction": 1.0,
                  "reset_time": "2026-06-23T02:43:38Z"
                },
                {
                  "window": "5h",
                  "remaining_fraction": 0.70,
                  "reset_time": "2026-06-16T07:26:50Z"
                }
              ]
            }
          ]
        }
        """;

    [Fact]
    public void ParseQuotaSummary_maps_gemini_and_third_party_groups()
    {
        using var document = JsonDocument.Parse(SampleQuotaSummaryJson);
        var snapshot = AntigravityUsageClient.ParseQuotaSummary(document.RootElement, "Pro");

        Assert.True(snapshot.IsAvailable);
        Assert.Equal("Pro", snapshot.PlanLabel);
        Assert.True(snapshot.Gemini.IsAvailable);
        Assert.Equal(95, snapshot.Gemini.SessionPercentRemaining);
        Assert.Equal(98, snapshot.Gemini.WeeklyPercentRemaining);
        Assert.True(snapshot.ThirdParty.IsAvailable);
        Assert.Equal(70, snapshot.ThirdParty.SessionPercentRemaining);
        Assert.Equal(100, snapshot.ThirdParty.WeeklyPercentRemaining);
    }

    [Fact]
    public void ParseRemainingFraction_treats_reset_only_bucket_as_exhausted()
    {
        using var document = JsonDocument.Parse("""{"reset_time":"2026-06-23T02:43:38Z"}""");
        Assert.Equal(0, AntigravityUsageClient.ParseRemainingFraction(document.RootElement));
    }

    [Fact]
    public void ParseRemainingFraction_supports_camelCase_property()
    {
        using var document = JsonDocument.Parse("""{"remainingFraction":0.5}""");
        Assert.Equal(50, AntigravityUsageClient.ParseRemainingFraction(document.RootElement));
    }

    [Fact]
    public void IsGeminiGroup_matches_display_name()
    {
        Assert.True(AntigravityUsageClient.IsGeminiGroup("Gemini Models"));
        Assert.False(AntigravityUsageClient.IsGeminiGroup("Claude and GPT models"));
    }

    [Fact]
    public void IsThirdPartyGroup_matches_display_name()
    {
        Assert.True(AntigravityUsageClient.IsThirdPartyGroup("Claude and GPT models"));
        Assert.False(AntigravityUsageClient.IsThirdPartyGroup("Gemini Models"));
    }

    [Fact]
    public void ParseOAuthEnvelope_returns_empty_for_invalid_value()
    {
        var tokens = AntigravityTokenReader.ParseOAuthEnvelope("not-valid-base64!!!");
        Assert.Null(tokens.AccessToken);
        Assert.Null(tokens.RefreshToken);
    }

    [Fact]
    public void ParseUserQuota_maps_per_model_buckets_to_gemini_group()
    {
        const string json = """
            {
              "buckets": [
                {
                  "modelId": "gemini-2.5-pro",
                  "remainingFraction": 0.965,
                  "resetTime": "2026-07-01T12:00:00Z"
                },
                {
                  "modelId": "gemini-2.5-flash",
                  "remainingFraction": 0.50,
                  "resetTime": "2026-07-08T12:00:00Z"
                }
              ]
            }
            """;

        using var document = JsonDocument.Parse(json);
        var snapshot = AntigravityUsageClient.ParseUserQuota(document.RootElement, "Pro");

        Assert.True(snapshot.IsAvailable);
        Assert.Equal("Pro", snapshot.PlanLabel);
        Assert.True(snapshot.Gemini.IsAvailable);
        Assert.Equal(96.5, snapshot.Gemini.SessionPercentRemaining);
        Assert.Equal(50, snapshot.Gemini.WeeklyPercentRemaining);
        Assert.False(snapshot.ThirdParty.IsAvailable);
    }

    [Fact]
    public void IsGeminiModelId_matches_gemini_models()
    {
        Assert.True(AntigravityUsageClient.IsGeminiModelId("gemini-2.5-pro"));
        Assert.False(AntigravityUsageClient.IsGeminiModelId("gpt-4o"));
    }

    [Fact]
    public void IsAccessTokenValid_returns_false_when_expired()
    {
        var tokens = new AntigravityOAuthTokens
        {
            AccessToken = "ya29.test",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };

        Assert.False(tokens.IsAccessTokenValid());
    }
}
