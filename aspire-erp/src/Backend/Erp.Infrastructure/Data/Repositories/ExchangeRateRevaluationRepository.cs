using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

public class ExchangeRateRevaluationRepository : IExchangeRateRevaluationRepository
{
    private readonly AppDbContext _context;

    public ExchangeRateRevaluationRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ExchangeRateRevaluation revaluation, CancellationToken cancellationToken = default)
    {
        await _context.ExchangeRateRevaluations.AddAsync(revaluation, cancellationToken);
    }

        public async Task<List<ExchangeRateRevaluation>> GetByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        return await _context.ExchangeRateRevaluations
            .Where(x => x.CompanyId == companyId)
            .OrderByDescending(x => x.PostingDate)
            .ToListAsync(cancellationToken);
    }
public async Task<ExchangeRateRevaluation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ExchangeRateRevaluations
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task UpdateAsync(ExchangeRateRevaluation revaluation, CancellationToken cancellationToken = default)
    {
        _context.ExchangeRateRevaluations.Update(revaluation);
        return Task.CompletedTask;
    }

    public async Task<List<ForeignCurrencyBalance>> GetForeignCurrencyBalancesAsync(Guid companyId, DateOnly upToDate, CancellationToken cancellationToken = default)
    {
        var query = from gl in _context.GLEntries
                    join acc in _context.Accounts on gl.AccountId equals acc.Id
                    where acc.CompanyId == companyId 
                       && acc.CurrencyId != null 
                       && gl.PostingDate <= upToDate
                       && !gl.IsCancelled
                       && !acc.IsGroup
                       && (acc.RootType == AccountRootType.Asset || acc.RootType == AccountRootType.Liability)
                       && acc.Type != AccountType.Stock
                    group gl by new { gl.AccountId, acc.CurrencyId } into g
                    select new ForeignCurrencyBalance
                    {
                        AccountId = g.Key.AccountId,
                        CurrencyId = g.Key.CurrencyId!.Value,
                        BalanceInBaseCurrency = g.Sum(x => x.Debit - x.Credit),
                        BalanceInForeignCurrency = g.Sum(x => x.DebitInAccountCurrency - x.CreditInAccountCurrency)
                    };

        var balances = await query.ToListAsync(cancellationToken);
        
        return balances.Where(x => x.BalanceInBaseCurrency != 0 || x.BalanceInForeignCurrency != 0).ToList();
    }
}
