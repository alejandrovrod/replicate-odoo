using System.Text.Json.Serialization;
using Erp.Application.Common;


namespace Erp.Application.Features.ExchangeRates.Commands;

public record UpsertExchangeRateCommand : ICommand<Result<UpsertExchangeRateResult>>
{
    [JsonIgnore]
    public string IdempotencyKey { get; init; } = string.Empty;

    public Guid FromCurrencyId { get; init; }
    public Guid ToCurrencyId { get; init; }
    public DateOnly RateDate { get; init; }
    public decimal Rate { get; init; }
    
    public byte[]? RowVersion { get; init; }
}

public record UpsertExchangeRateResult(Guid Id, byte[] RowVersion);
