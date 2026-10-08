using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.ExchangeRates.Queries;

public record GetRevaluationPreviewQuery(
    Guid CompanyId,
    DateOnly PostingDate,
    decimal RoundingLossAllowance,
    Guid? ExchangeGainLossAccountId) : IQuery<Result<RevaluationPreviewDto>>;

public record RevaluationPreviewDto(
    Guid CompanyId,
    DateOnly PostingDate,
    List<RevaluationPreviewLineDto> Lines,
    decimal TotalGainLoss);

public record RevaluationPreviewLineDto(
    Guid AccountId,
    Guid CurrencyId,
    decimal ForeignBalance,
    decimal BaseBalance,
    decimal NewExchangeRate,
    decimal UnrealizedGainLoss);
