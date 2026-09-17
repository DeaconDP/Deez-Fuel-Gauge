using System.Text.Json;
using DeezFuelGauge.Services;

var settings = SettingsStore.Load();
settings.Cursor.ShowProLimits = true;
settings.Cursor.ShowProBreakdown = true;
settings.GrokBot.ShowProLimits = true;
using var refresh = new UsageRefreshService();
var result = await refresh.RefreshAsync(settings);
var s = result.Snapshot;
Console.WriteLine($"cursorPercent={s.PercentUsed}");
Console.WriteLine($"autoPercent={s.AutoPercentUsed}");
Console.WriteLine($"apiPercent={s.ApiPercentUsed}");
Console.WriteLine($"billingCycleStartMs={s.BillingCycleStartMs}");
Console.WriteLine($"billingCycleEndMs={s.BillingCycleEndMs}");
if (s.BillingCycleEndMs is long endMs)
{
    var end = DateTimeOffset.FromUnixTimeMilliseconds(endMs).ToOffset(TimeSpan.FromHours(2));
    Console.WriteLine($"billingCycleEndSAST={end:yyyy-MM-dd HH:mm} SAST");
}
if (s.BillingCycleStartMs is long startMs)
{
    var start = DateTimeOffset.FromUnixTimeMilliseconds(startMs).ToOffset(TimeSpan.FromHours(2));
    Console.WriteLine($"billingCycleStartSAST={start:yyyy-MM-dd HH:mm} SAST");
}
Console.WriteLine($"grokPercent={s.GrokBot.PercentUsed}");
Console.WriteLine($"grokResetsAt={s.GrokBot.ResetsAt}");
if (s.GrokBot.ResetsAt is DateTimeOffset r)
    Console.WriteLine($"grokResetsAtSAST={r.ToOffset(TimeSpan.FromHours(2)):yyyy-MM-dd HH:mm} SAST");
