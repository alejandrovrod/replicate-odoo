using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

public interface IExchangeRateRepository
{
    Task<ExchangeRate?> GetLatestRateAsync(Guid fromCurrencyId, Guid toCurrencyId, DateOnly rateDate, CancellationToken cancellationToken = default);
    Task<ExchangeRate?> GetExactRateAsync(Guid fromCurrencyId, Guid toCurrencyId, DateOnly rateDate, CancellationToken cancellationToken = default);
    Task AddAsync(ExchangeRate exchangeRate, CancellationToken cancellationToken = default);
    Task<ExchangeRate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
