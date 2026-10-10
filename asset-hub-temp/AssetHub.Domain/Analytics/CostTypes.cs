namespace AssetHub.Domain.Analytics;

public static class CostTypes
{
    public const string Labor = "labor";
    public const string Parts = "parts";
    public const string Downtime = "downtime";
    public const string ExternalServices = "external_services";
    public const string Penalty = "penalty";
    public const string Other = "other";

    public static readonly HashSet<string> All = new()
    {
        Labor, Parts, Downtime, ExternalServices, Penalty, Other
    };
}
