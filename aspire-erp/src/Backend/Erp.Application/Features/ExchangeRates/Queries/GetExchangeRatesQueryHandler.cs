using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.ExchangeRates.Queries;

public class GetExchangeRatesQueryHandler : IQueryHandler<GetExchangeRatesQuery, IReadOnlyList<ExchangeRateDto>>
{
    private readonly IExchangeRateRepository _repository;

    public GetExchangeRatesQueryHandler(IExchangeRateRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<ExchangeRateDto>> HandleAsync(GetExchangeRatesQuery request, CancellationToken cancellationToken)
    {
        var rates = await _repository.GetAllAsync(cancellationToken);
        
        return rates.Select(x => new ExchangeRateDto
        {
            Id = x.Id,
            FromCurrencyId = x.FromCurrencyId,
            ToCurrencyId = x.ToCurrencyId,
            RateDate = x.RateDate,
            Rate = x.Rate,
            RowVersion = Convert.ToBase64String(x.RowVersion)
        }).ToList();
    }
}
