namespace AssetHub.Api.Configuration;

public class SchedulerSettings
{
    public const string SectionName = "SchedulerSettings";
    public string ApiKey { get; set; } = string.Empty;
    public int IntervalMinutes { get; set; } = 1;
}
