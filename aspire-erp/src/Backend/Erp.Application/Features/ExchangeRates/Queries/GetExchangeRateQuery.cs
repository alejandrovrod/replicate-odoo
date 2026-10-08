using Erp.Application.Common;

namespace Erp.Application.Features.ExchangeRates.Queries;

public record GetExchangeRateQuery(Guid FromCurrencyId, Guid ToCurrencyId, DateOnly RateDate) : IQuery<Result<GetExchangeRateResult>>;

public record GetExchangeRateResult(decimal EffectiveRate, bool IsInverse);
