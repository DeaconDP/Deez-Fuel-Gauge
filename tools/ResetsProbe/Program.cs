using System.Text.Json;
using System.Text.Json.Serialization;
using DeezFuelGauge.Services;

var sast = TimeSpan.FromHours(2);
string? Fmt(DateTimeOffset? dto) =>
    dto is DateTimeOffset d ? d.ToOffset(sast).ToString("yyyy-MM-dd HH:mm") + " SAST" : null;

var settings = SettingsStore.Load();
settings.Claude.ShowProLimits = true;
settings.OpenAi.ShowProLimits = true;
settings.GrokBot.ShowProLimits = true;
settings.Xai.ShowProLimits = true;
settings.Cursor.ShowProLimits = true;
settings.Cursor.ShowProBreakdown = true;

using var refresh = new UsageRefreshService();
var result = await refresh.RefreshAsync(settings);
var s = result.Snapshot;

string? cursorReset = null;
if (s.BillingCycleEndMs is long endMs)
    cursorReset = DateTimeOffset.FromUnixTimeMilliseconds(endMs).ToOffset(sast).ToString("yyyy-MM-dd HH:mm") + " SAST";

double? cursorModelsPct = null;
if (!s.IsError)
    cursorModelsPct = Math.Round(s.AutoPercentUsed ?? s.PercentUsed, 1);

var payload = new Dictionary<string, object?>
{
    ["refreshedAt"] = result.RefreshedAt.UtcDateTime.ToString("o"),
    ["machine"] = Environment.MachineName,
    ["claude5h"] = new { pct = s.ClaudePro.IsAvailable ? (double?)Math.Round(s.ClaudePro.SessionPercentUsed, 1) : null, available = s.ClaudePro.IsAvailable, status = s.ClaudePro.StatusMessage, resets = Fmt(s.ClaudePro.SessionResetsAt) },
    ["claudeWeekly"] = new { pct = s.ClaudePro.IsAvailable ? (double?)Math.Round(s.ClaudePro.WeeklyPercentUsed, 1) : null, available = s.ClaudePro.IsAvailable, status = s.ClaudePro.StatusMessage, resets = Fmt(s.ClaudePro.WeeklyResetsAt) },
    ["openai5h"] = new { pct = (s.Codex.IsAvailable && s.Codex.HasSessionWindow) ? (double?)Math.Round(s.Codex.SessionPercentUsed, 1) : null, available = s.Codex.IsAvailable && s.Codex.HasSessionWindow, status = s.Codex.StatusMessage, resets = Fmt(s.Codex.SessionResetsAt) },
    ["openaiWeekly"] = new { pct = (s.Codex.IsAvailable && s.Codex.HasWeeklyWindow) ? (double?)Math.Round(s.Codex.WeeklyPercentUsed, 1) : null, available = s.Codex.IsAvailable && s.Codex.HasWeeklyWindow, status = s.Codex.StatusMessage, resets = Fmt(s.Codex.WeeklyResetsAt) },
    ["cursorModels"] = new { pct = cursorModelsPct, available = !s.IsError, status = s.ErrorMessage, resets = cursorReset },
    ["cursorApi"] = new { pct = (!s.IsError && s.ApiPercentUsed is not null) ? (double?)Math.Round(s.ApiPercentUsed.Value, 1) : null, available = !s.IsError && s.ApiPercentUsed is not null, status = s.ErrorMessage, resets = cursorReset },
    ["grokBot"] = new { pct = s.GrokBot.IsAvailable ? (double?)Math.Round(s.GrokBot.PercentUsed, 1) : null, available = s.GrokBot.IsAvailable, status = s.GrokBot.StatusMessage, resets = Fmt(s.GrokBot.ResetsAt) },
    ["xai"] = new { pct = s.Xai.IsAvailable ? (double?)Math.Round(s.Xai.HeadlinePercentUsed, 1) : null, available = s.Xai.IsAvailable, status = s.Xai.StatusMessage, balanceUsd = s.Xai.BalanceUsd, prepaidTotalUsd = s.Xai.PrepaidTotalUsd, prepaidUsedUsd = s.Xai.PrepaidUsedUsd, detail = s.Xai.DetailLabel },
};

Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }));
