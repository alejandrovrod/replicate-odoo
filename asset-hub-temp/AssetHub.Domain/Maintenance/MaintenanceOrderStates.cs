namespace AssetHub.Domain.Maintenance;

public static class MaintenanceOrderStates
{
    public const string Draft = "draft";
    public const string Approved = "approved";
    public const string Scheduled = "scheduled";
    public const string InProgress = "in_progress";
    public const string Done = "done";
    public const string Rescheduled = "rescheduled";
    public const string Verified = "verified";
    public const string Cancelled = "cancelled";

    public static readonly HashSet<string> ActiveStates = new()
    {
        Draft, Approved, Scheduled, InProgress, Rescheduled
    };

    public static readonly HashSet<string> TerminalStates = new()
    {
        Verified, Cancelled
    };
}
