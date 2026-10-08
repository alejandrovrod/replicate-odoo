using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.ExchangeRates.Queries;

public record GetExchangeRateRevaluationDetailQuery(Guid Id) : IQuery<Result<ExchangeRateRevaluationDetailDto>>;
