using Erp.Application.Common;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;


namespace Erp.Application.Features.ExchangeRates.Queries;

public class GetExchangeRateQueryHandler : IQueryHandler<GetExchangeRateQuery, Result<GetExchangeRateResult>>
{
    private readonly IExchangeRateRepository _repository;

    public GetExchangeRateQueryHandler(IExchangeRateRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<GetExchangeRateResult>> HandleAsync(GetExchangeRateQuery request, CancellationToken cancellationToken)
    {
        if (request.FromCurrencyId == request.ToCurrencyId)
        {
            return Result<GetExchangeRateResult>.Success(new GetExchangeRateResult(1m, false));
        }

        var rateRow = await _repository.GetLatestRateAsync(request.FromCurrencyId, request.ToCurrencyId, request.RateDate, cancellationToken);
        
        if (rateRow != null)
        {
            bool isInverse = rateRow.FromCurrencyId != request.FromCurrencyId;
            return Result<GetExchangeRateResult>.Success(new GetExchangeRateResult(rateRow.Rate, isInverse));
        }

        return Result<GetExchangeRateResult>.Failure("exchange_rate_missing", $"No exchange rate found between '{request.FromCurrencyId}' and '{request.ToCurrencyId}' on or before {request.RateDate:yyyy-MM-dd}.");
    }
}
