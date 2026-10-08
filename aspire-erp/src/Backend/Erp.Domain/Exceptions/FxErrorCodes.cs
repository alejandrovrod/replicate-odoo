namespace Erp.Domain.Exceptions;

public static class FxErrorCodes
{
    public const string ExchangeRateSelfPair = "exchange_rate_self_pair";
    public const string ExchangeRateInvalid = "exchange_rate_invalid";
    public const string ExchangeRateMissing = "exchange_rate_missing";
    public const string InvalidRoundingLossAllowance = "invalid_rounding_loss_allowance";
    public const string RevaluationAlreadyCancelled = "revaluation_already_cancelled";
    public const string RevaluationInvalidTransition = "revaluation_invalid_transition";
    public const string RevaluationFutureDate = "revaluation_future_date";
    public const string InvalidExchangeGainLossAccount = "invalid_exchange_gain_loss_account";
    public const string NoRevaluationGainLoss = "no_revaluation_gain_loss";
}

public class FxValidationException : Exception
{
    public string Code { get; }

    public FxValidationException(string code, string message) : base(message)
    {
        Code = code;
    }
}
