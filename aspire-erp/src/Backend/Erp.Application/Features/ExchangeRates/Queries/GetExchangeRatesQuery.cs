using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.ExchangeRates.Queries;

public record GetExchangeRatesQuery() : IQuery<IReadOnlyList<ExchangeRateDto>>;
