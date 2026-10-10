namespace AssetHub.Domain.Incidents;

public static class IncidentStates
{
    public const string Reported = "reported";
    public const string Triaged = "triaged";
    public const string Assigned = "assigned";
    public const string InProgress = "in_progress";
    public const string Resolved = "resolved";
    public const string Closed = "closed";
    public const string Cancelled = "cancelled";

    public static readonly HashSet<string> TerminalStates = new()
    {
        Closed, Cancelled
    };

    public static readonly HashSet<string> ActiveStates = new()
    {
        Reported, Triaged, Assigned, InProgress
    };
}
