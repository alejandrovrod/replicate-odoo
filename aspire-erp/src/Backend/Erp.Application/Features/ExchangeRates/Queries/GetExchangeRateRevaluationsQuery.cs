using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.ExchangeRates.Queries;

public record GetExchangeRateRevaluationsQuery(Guid CompanyId, string? Status) : IQuery<Result<List<ExchangeRateRevaluationDto>>>;
