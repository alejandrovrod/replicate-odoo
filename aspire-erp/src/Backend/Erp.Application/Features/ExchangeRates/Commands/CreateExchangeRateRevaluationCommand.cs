using System.Text.Json.Serialization;
using Erp.Application.Common;


namespace Erp.Application.Features.ExchangeRates.Commands;

public record CreateExchangeRateRevaluationCommand : ICommand<Result<CreateExchangeRateRevaluationResult>>
{
    [JsonIgnore]
    public string IdempotencyKey { get; init; } = string.Empty;

    public Guid CompanyId { get; init; }
    public DateOnly PostingDate { get; init; }
    public decimal RoundingLossAllowance { get; init; }
    public Guid? ExchangeGainLossAccountId { get; init; }
    public string? Remarks { get; init; }
}

public record CreateExchangeRateRevaluationResult(Guid Id);
