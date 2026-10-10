namespace AssetHub.Domain.Exceptions;

public class PlanLimitExceededException : DomainException
{
    public PlanLimitExceededException(string limitType, string detail)
        : base(
            "plan_limit_exceeded",
            $"Limit exceeded for: {limitType}. {detail}",
            "Domain.PlanLimitExceeded",
            limitType,
            detail)
    {
    }
}
