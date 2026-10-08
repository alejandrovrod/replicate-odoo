using System.Text.Json.Serialization;
using Erp.Application.Common;

namespace Erp.Application.Features.ExchangeRates.Commands;

public record CancelExchangeRateRevaluationCommand : ICommand<Result<CancelExchangeRateRevaluationResult>>
{
    [JsonIgnore]
    public string IdempotencyKey { get; init; } = string.Empty;

    public Guid RevaluationId { get; init; }
    
    public byte[]? RowVersion { get; init; }
}

public record CancelExchangeRateRevaluationResult(Guid Id);
