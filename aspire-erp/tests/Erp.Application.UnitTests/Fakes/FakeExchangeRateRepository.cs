using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

public sealed class FakeExchangeRateRepository : IExchangeRateRepository
{
    public List<ExchangeRate> Rates { get; } = new();

    public Task<ExchangeRate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Rates.FirstOrDefault(r => r.Id == id));
    }

    public Task<IReadOnlyList<ExchangeRate>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ExchangeRate>>(Rates.ToList());
    }

    public Task<ExchangeRate?> GetLatestRateAsync(Guid fromCurrencyId, Guid toCurrencyId, DateOnly date, CancellationToken cancellationToken = default)
    {
        if (fromCurrencyId == toCurrencyId)
        {
            return Task.FromResult<ExchangeRate?>(new ExchangeRate
            {
                FromCurrencyId = fromCurrencyId,
                ToCurrencyId = toCurrencyId,
                RateDate = date,
                Rate = 1m
            });
        }

        var direct = Rates
            .Where(r => r.FromCurrencyId == fromCurrencyId && r.ToCurrencyId == toCurrencyId && r.RateDate <= date)
            .OrderByDescending(r => r.RateDate)
            .FirstOrDefault();
        if (direct != null) return Task.FromResult<ExchangeRate?>(direct);

        var inverse = Rates
            .Where(r => r.FromCurrencyId == toCurrencyId && r.ToCurrencyId == fromCurrencyId && r.RateDate <= date)
            .OrderByDescending(r => r.RateDate)
            .FirstOrDefault();
        if (inverse != null)
        {
            return Task.FromResult<ExchangeRate?>(new ExchangeRate
            {
                Id = inverse.Id,
                FromCurrencyId = inverse.FromCurrencyId,
                ToCurrencyId = inverse.ToCurrencyId,
                RateDate = inverse.RateDate,
                Rate = Math.Round(1m / inverse.Rate, 6)
            });
        }
        return Task.FromResult<ExchangeRate?>(null);
    }

    public Task<IReadOnlyList<ExchangeRate>> GetRatesInPeriodAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ExchangeRate>>(Rates.Where(r => r.RateDate >= startDate && r.RateDate <= endDate).ToList());
    }

    public Task AddAsync(ExchangeRate rate, CancellationToken cancellationToken = default)
    {
        Rates.Add(rate);
        return Task.CompletedTask;
    }

    public Task<ExchangeRate?> GetExactRateAsync(Guid fromCurrencyId, Guid toCurrencyId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var direct = Rates.FirstOrDefault(r => r.FromCurrencyId == fromCurrencyId && r.ToCurrencyId == toCurrencyId && r.RateDate == date);
        return Task.FromResult<ExchangeRate?>(direct);
    }

    public Task UpdateAsync(ExchangeRate rate, CancellationToken cancellationToken = default)
    {
        var existing = Rates.FirstOrDefault(r => r.Id == rate.Id);
        if (existing != null)
        {
            Rates.Remove(existing);
            Rates.Add(rate);
        }
        return Task.CompletedTask;
    }

    public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        return operation(cancellationToken);
    }
}


