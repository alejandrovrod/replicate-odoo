using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

public class ExchangeRate
{
    public Guid Id { get; set; }
    public Guid FromCurrencyId { get; set; }
    public Currency? FromCurrency { get; set; }
    public Guid ToCurrencyId { get; set; }
    public Currency? ToCurrency { get; set; }
    public DateOnly RateDate { get; set; }
    public decimal Rate { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public DateTimeOffset CreatedAt { get; set; }

    public void EnsureValid()
    {
        if (FromCurrencyId == ToCurrencyId)
        {
            throw new FxValidationException(FxErrorCodes.ExchangeRateSelfPair, "Cannot define an exchange rate between the same currency.");
        }

        if (Rate <= 0)
        {
            throw new FxValidationException(FxErrorCodes.ExchangeRateInvalid, "Exchange rate must be strictly greater than zero.");
        }
    }
}

public static class ExchangeRateValidator
{
    public static void EnsureValidAllowance(decimal allowance)
    {
        if (allowance < 0 || allowance >= 1)
        {
            throw new FxValidationException(FxErrorCodes.InvalidRoundingLossAllowance, "Rounding loss allowance must be in the range [0, 1).");
        }
    }
}
