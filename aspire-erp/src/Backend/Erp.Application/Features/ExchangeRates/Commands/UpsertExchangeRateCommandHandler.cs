using Erp.Application.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.ExchangeRates.Commands;

public class UpsertExchangeRateCommandHandler : ICommandHandler<UpsertExchangeRateCommand, Result<UpsertExchangeRateResult>>
{
    private readonly IExchangeRateRepository _repository;

    public UpsertExchangeRateCommandHandler(IExchangeRateRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<UpsertExchangeRateResult>> HandleAsync(UpsertExchangeRateCommand request, CancellationToken cancellationToken)
    {
        var rate = await _repository.GetExactRateAsync(request.FromCurrencyId, request.ToCurrencyId, request.RateDate, cancellationToken);
        if (rate == null)
        {
            rate = new ExchangeRate
            {
                FromCurrencyId = request.FromCurrencyId,
                ToCurrencyId = request.ToCurrencyId,
                RateDate = request.RateDate,
                Rate = request.Rate
            };
            
            rate.EnsureValid();
            await _repository.AddAsync(rate, cancellationToken);
        }
        else
        {
            if (request.RowVersion != null && !rate.RowVersion.SequenceEqual(request.RowVersion))
            {
                throw new ConcurrencyConflictException("ExchangeRate", rate.Id);
            }
            
            rate.Rate = request.Rate;
            rate.EnsureValid();
        }

        return Result<UpsertExchangeRateResult>.Success(new UpsertExchangeRateResult(rate.Id, rate.RowVersion));
    }
}
