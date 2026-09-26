using System.Text.Json;

namespace CxCell;

public static class QuotaParser
{
    private const int FiveHoursMinutes = 5 * 60;
    private const int WeekMinutes = 7 * 24 * 60;

    public static QuotaSnapshot Parse(JsonElement result)
    {
        var snapshot = SelectCodexSnapshot(result);
        if (snapshot.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return new QuotaSnapshot(null, null, null);
        }

        string? planType = TryGetString(snapshot, "planType");
        var primary = ParseWindow(snapshot, "primary");
        var secondary = ParseWindow(snapshot, "secondary");

        QuotaWindow? fiveHour = null;
        QuotaWindow? weekly = null;

        foreach (var window in new[] { primary, secondary })
        {
            if (window is null)
            {
                continue;
            }

            var kind = Classify(window.Value.DurationMinutes);
            if (kind == QuotaKind.FiveHour)
            {
                fiveHour ??= ToQuotaWindow(kind.Value, window.Value);
            }
            else if (kind == QuotaKind.Weekly)
            {
                weekly ??= ToQuotaWindow(kind.Value, window.Value);
            }
        }

        // Older/partial responses may omit duration. Only use positional fallback when both
        // windows exist, which avoids fabricating a 5-hour bucket for plans where it is absent.
        if (primary is not null && secondary is not null)
        {
            fiveHour ??= ToQuotaWindow(QuotaKind.FiveHour, primary.Value);
            weekly ??= ToQuotaWindow(QuotaKind.Weekly, secondary.Value);
        }
        else if (primary is not null && weekly is null && fiveHour is null)
        {
            // A single known-duration window can still be classified. Unknown single windows
            // are intentionally omitted instead of guessing.
            var kind = Classify(primary.Value.DurationMinutes);
            if (kind == QuotaKind.FiveHour)
                fiveHour = ToQuotaWindow(kind.Value, primary.Value);
            else if (kind == QuotaKind.Weekly)
                weekly = ToQuotaWindow(kind.Value, primary.Value);
        }

        return new QuotaSnapshot(planType, fiveHour, weekly);
    }

    private static JsonElement SelectCodexSnapshot(JsonElement result)
    {
        if (result.TryGetProperty("rateLimitsByLimitId", out var byId) &&
            byId.ValueKind == JsonValueKind.Object &&
            byId.TryGetProperty("codex", out var codex))
        {
            return codex;
        }

        return result.TryGetProperty("rateLimits", out var rateLimits)
            ? rateLimits
            : default;
    }

    private static (double UsedPercent, int? DurationMinutes, DateTimeOffset? ResetsAt)? ParseWindow(
        JsonElement snapshot,
        string propertyName)
    {
        if (!snapshot.TryGetProperty(propertyName, out var element) ||
            element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (!element.TryGetProperty("usedPercent", out var usedElement) ||
            usedElement.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        var usedPercent = usedElement.GetDouble();
        int? duration = null;
        if (element.TryGetProperty("windowDurationMins", out var durationElement) &&
            durationElement.ValueKind == JsonValueKind.Number)
        {
            duration = durationElement.GetInt32();
        }

        DateTimeOffset? resetsAt = null;
        if (element.TryGetProperty("resetsAt", out var resetElement) &&
            resetElement.ValueKind == JsonValueKind.Number &&
            resetElement.TryGetInt64(out var unixSeconds))
        {
            resetsAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }

        return (usedPercent, duration, resetsAt);
    }

    private static QuotaKind? Classify(int? durationMinutes)
    {
        if (durationMinutes is null)
            return null;

        if (Math.Abs(durationMinutes.Value - FiveHoursMinutes) <= 90)
            return QuotaKind.FiveHour;

        if (Math.Abs(durationMinutes.Value - WeekMinutes) <= 24 * 60)
            return QuotaKind.Weekly;

        return null;
    }

    private static QuotaWindow ToQuotaWindow(
        QuotaKind kind,
        (double UsedPercent, int? DurationMinutes, DateTimeOffset? ResetsAt) value)
    {
        var remaining = Math.Clamp(100d - value.UsedPercent, 0d, 100d);
        return new QuotaWindow(kind, remaining, value.ResetsAt, value.DurationMinutes);
    }

    private static string? TryGetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
