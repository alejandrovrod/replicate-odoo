using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.ExchangeRates.Queries;

public class GetExchangeRateRevaluationsQueryHandler : IQueryHandler<GetExchangeRateRevaluationsQuery, Result<List<ExchangeRateRevaluationDto>>>
{
    private readonly IExchangeRateRevaluationRepository _repository;

    public GetExchangeRateRevaluationsQueryHandler(IExchangeRateRevaluationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<List<ExchangeRateRevaluationDto>>> HandleAsync(GetExchangeRateRevaluationsQuery request, CancellationToken cancellationToken)
    {
        var items = await _repository.GetByCompanyAsync(request.CompanyId, cancellationToken);
        if (!string.IsNullOrEmpty(request.Status))
        {
            items = items.Where(x => x.DocumentStatus.ToString().Equals(request.Status, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return Result<List<ExchangeRateRevaluationDto>>.Success(items.Select(x => new ExchangeRateRevaluationDto(
            x.Id,
            x.CompanyId,
            x.VoucherNo,
            x.PostingDate,
            x.ExchangeGainLossAccountId,
            x.RoundingLossAllowance,
            x.DocumentStatus.ToString(),
            x.Remarks,
            x.RowVersion
        )).ToList());
    }
}
