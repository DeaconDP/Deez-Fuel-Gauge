using System.Text.Json;
using System.Text.Json.Serialization;
using DeezFuelGauge.Models;
using DeezFuelGauge.Services;

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

var providers = new List<object>
{
    Row("claude-5h", "Claude 5h", s.ClaudePro.IsAvailable, s.ClaudePro.SessionPercentUsed, s.ClaudePro.StatusMessage),
    Row("claude-daily", "Claude Daily", s.ClaudePro.IsAvailable, s.ClaudePro.WeeklyPercentUsed, s.ClaudePro.StatusMessage),
    Row("openai-5h", "OpenAI 5h", s.Codex.IsAvailable && s.Codex.HasSessionWindow, s.Codex.SessionPercentUsed, s.Codex.StatusMessage),
    Row("openai-daily", "OpenAI Daily", s.Codex.IsAvailable && s.Codex.HasWeeklyWindow, s.Codex.WeeklyPercentUsed, s.Codex.StatusMessage),
    Row("cursor-models-monthly", "Cursor Models Monthly", !s.IsError, s.AutoPercentUsed ?? s.PercentUsed, s.ErrorMessage),
    Row("cursor-api", "Cursor API", !s.IsError && s.ApiPercentUsed is not null, s.ApiPercentUsed ?? 0, s.ErrorMessage),
    Row("grok-bot", "Grok-Bot", s.GrokBot.IsAvailable, s.GrokBot.PercentUsed, s.GrokBot.StatusMessage),
    Row("xai-api", "xAI API", s.Xai.IsAvailable, s.Xai.HeadlinePercentUsed, s.Xai.StatusMessage),
};

object Row(string id, string label, bool available, double percent, string? status) => new
{
    id,
    label,
    percentUsed = available ? Math.Round(Math.Clamp(percent, 0, 100), 1) : (double?)null,
    available,
    status = string.IsNullOrWhiteSpace(status) ? (available ? "ok" : "unavailable") : status
};

var payload = new
{
    refreshedAt = result.RefreshedAt.UtcDateTime.ToString("o"),
    machine = Environment.MachineName,
    providers
};

var dir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "deez-fuel-gauge");
Directory.CreateDirectory(dir);
var path = Path.Combine(dir, "latest.json");
var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
{
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
});
await File.WriteAllTextAsync(path, json);
Console.WriteLine(path);
Console.WriteLine(json);
