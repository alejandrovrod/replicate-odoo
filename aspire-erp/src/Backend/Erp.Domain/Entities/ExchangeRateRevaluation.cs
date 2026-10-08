using Erp.Domain.Common;
using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

public class ExchangeRateRevaluation : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }

    public string VoucherNo { get; set; } = string.Empty;
    public DateOnly PostingDate { get; set; }
    
    public Guid? ExchangeGainLossAccountId { get; set; }
    public Account? ExchangeGainLossAccount { get; set; }

    public decimal RoundingLossAllowance { get; set; }
    public DocumentStatus DocumentStatus { get; set; }
    public string? Remarks { get; set; }
    public string? IdempotencyKey { get; set; }

    public ICollection<ExchangeRateRevaluationLine> Lines { get; set; } = new List<ExchangeRateRevaluationLine>();

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public DateTimeOffset CreatedAt { get; set; }

    public void EnsureCanSubmit(DateOnly today)
    {
        if (DocumentStatus == DocumentStatus.Cancelled)
        {
            throw new FxValidationException(FxErrorCodes.RevaluationAlreadyCancelled, $"Revaluation '{VoucherNo}' is already cancelled.");
        }

        if (DocumentStatus != DocumentStatus.Draft)
        {
            throw new FxValidationException(FxErrorCodes.RevaluationInvalidTransition, $"Revaluation '{VoucherNo}' cannot be submitted from status '{DocumentStatus}'.");
        }

        if (PostingDate > today)
        {
            throw new FxValidationException(FxErrorCodes.RevaluationFutureDate, $"Revaluation posting date {PostingDate} cannot be in the future.");
        }
        
        ExchangeRateValidator.EnsureValidAllowance(RoundingLossAllowance);
    }

    public void EnsureCanCancel()
    {
        if (DocumentStatus == DocumentStatus.Cancelled)
        {
            throw new FxValidationException(FxErrorCodes.RevaluationAlreadyCancelled, $"Revaluation '{VoucherNo}' is already cancelled.");
        }

        if (DocumentStatus != DocumentStatus.Submitted)
        {
            throw new FxValidationException(FxErrorCodes.RevaluationInvalidTransition, $"Revaluation '{VoucherNo}' cannot be cancelled from status '{DocumentStatus}'.");
        }
    }
}

public class ExchangeRateRevaluationLine : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ExchangeRateRevaluationId { get; set; }
    public ExchangeRateRevaluation? ExchangeRateRevaluation { get; set; }

    public Guid AccountId { get; set; }
    public Account? Account { get; set; }

    public Guid CurrencyId { get; set; }
    public Currency? Currency { get; set; }

    public decimal BalanceInForeignCurrency { get; set; }
    public decimal BalanceInBaseCurrency { get; set; }
    
    public decimal CurrentExchangeRate { get; set; }
    public decimal NewExchangeRate { get; set; }
    
    public decimal GainLossAmount { get; set; }

    public void EnsureValid()
    {
        if (GainLossAmount == 0)
        {
            throw new FxValidationException(FxErrorCodes.NoRevaluationGainLoss, "Revaluation line must have a non-zero gain or loss.");
        }
    }
}
