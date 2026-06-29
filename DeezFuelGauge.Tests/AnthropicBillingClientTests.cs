using System.Text.Json;
using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class AnthropicBillingClientTests
{
    [Fact]
    public void ParseCostResponse_converts_cents_to_dollars()
    {
        const string json = """
            {
              "data": [
                {
                  "results": [
                    { "amount": "1250" },
                    { "amount": "750" }
                  ]
                }
              ]
            }
            """;

        using var document = JsonDocument.Parse(json);
        var total = AnthropicBillingClient.ParseCostResponse(document.RootElement);

        Assert.Equal(20, total, 2);
    }

    [Fact]
    public void ParseUsageResponse_includes_cache_tokens_in_input()
    {
        const string json = """
            {
              "data": [
                {
                  "results": [
                    { "input_tokens": 100, "output_tokens": 20, "cache_read_input_tokens": 30 }
                  ]
                }
              ]
            }
            """;

        using var document = JsonDocument.Parse(json);
        var (input, output) = AnthropicBillingClient.ParseUsageResponse(document.RootElement);

        Assert.Equal(130, input);
        Assert.Equal(20, output);
    }
}
