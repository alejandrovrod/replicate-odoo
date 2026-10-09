using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

public class ExchangeRateRepository : IExchangeRateRepository
{
    private readonly AppDbContext _context;

    public ExchangeRateRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ExchangeRate?> GetLatestRateAsync(Guid fromCurrencyId, Guid toCurrencyId, DateOnly rateDate, CancellationToken cancellationToken = default)
    {
        if (fromCurrencyId == toCurrencyId)
        {
            return new ExchangeRate
            {
                FromCurrencyId = fromCurrencyId,
                ToCurrencyId = toCurrencyId,
                RateDate = rateDate,
                Rate = 1m
            };
        }

        var direct = await _context.ExchangeRates
            .Where(x => x.FromCurrencyId == fromCurrencyId && x.ToCurrencyId == toCurrencyId && x.RateDate <= rateDate)
            .OrderByDescending(x => x.RateDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (direct != null)
        {
            return direct;
        }

        var inverse = await _context.ExchangeRates
            .Where(x => x.FromCurrencyId == toCurrencyId && x.ToCurrencyId == fromCurrencyId && x.RateDate <= rateDate)
            .OrderByDescending(x => x.RateDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (inverse != null)
        {
            return new ExchangeRate
            {
                Id = inverse.Id,
                FromCurrencyId = inverse.FromCurrencyId,
                ToCurrencyId = inverse.ToCurrencyId,
                RateDate = inverse.RateDate,
                Rate = Math.Round(1m / inverse.Rate, 6)
            };
        }

        return null;
    }

    public async Task<ExchangeRate?> GetExactRateAsync(Guid fromCurrencyId, Guid toCurrencyId, DateOnly rateDate, CancellationToken cancellationToken = default)
    {
        return await _context.ExchangeRates
            .FirstOrDefaultAsync(x => x.FromCurrencyId == fromCurrencyId && x.ToCurrencyId == toCurrencyId && x.RateDate == rateDate, cancellationToken);
    }

    public async Task AddAsync(ExchangeRate exchangeRate, CancellationToken cancellationToken = default)
    {
        await _context.ExchangeRates.AddAsync(exchangeRate, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ExchangeRate exchangeRate, CancellationToken cancellationToken = default)
    {
        _context.ExchangeRates.Update(exchangeRate);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ExchangeRate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ExchangeRates.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ExchangeRate>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ExchangeRates.OrderByDescending(x => x.RateDate).ToListAsync(cancellationToken);
    }
}
