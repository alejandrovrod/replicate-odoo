using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

public class ForeignCurrencyBalance
{
    public Guid AccountId { get; set; }
    public Guid CurrencyId { get; set; }
    public decimal BalanceInForeignCurrency { get; set; }
    public decimal BalanceInBaseCurrency { get; set; }
}

public interface IExchangeRateRevaluationRepository
{
    Task AddAsync(ExchangeRateRevaluation revaluation, CancellationToken cancellationToken = default);
    Task<ExchangeRateRevaluation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<ExchangeRateRevaluation>> GetByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task UpdateAsync(ExchangeRateRevaluation revaluation, CancellationToken cancellationToken = default);
    Task<List<ForeignCurrencyBalance>> GetForeignCurrencyBalancesAsync(Guid companyId, DateOnly upToDate, CancellationToken cancellationToken = default);
}
