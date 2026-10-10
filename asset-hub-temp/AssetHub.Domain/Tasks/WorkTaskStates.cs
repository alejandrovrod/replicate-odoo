namespace AssetHub.Domain.Tasks;

public static class WorkTaskStates
{
    public const string Todo = "todo";
    public const string Rework = "rework";
    public const string InProgress = "in_progress";
    public const string Done = "done";
    public const string Cancelled = "cancelled";

    public static readonly HashSet<string> TerminalStates = new()
    {
        Done, Cancelled
    };
}
