namespace CxCell;

public enum QuotaKind
{
    FiveHour,
    Weekly
}

public sealed record QuotaWindow(
    QuotaKind Kind,
    double RemainingPercent,
    DateTimeOffset? ResetsAt,
    int? WindowDurationMinutes);

public sealed record QuotaSnapshot(
    string? PlanType,
    QuotaWindow? FiveHour,
    QuotaWindow? Weekly)
{
    public bool HasAnyQuota => FiveHour is not null || Weekly is not null;
}
