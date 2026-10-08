using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Seeders;

public static class ExchangeGainLossSeeder
{
    public static async Task SeedAsync(AppDbContext context, Guid companyId, Guid currentTenantId, CancellationToken cancellationToken = default)
    {
        var exists = await context.Accounts
            .IgnoreQueryFilters()
            .AnyAsync(a => a.TenantId == currentTenantId && a.CompanyId == companyId && a.AccountCode == "5120-FX", cancellationToken);
            
        if (!exists)
        {
            var fxAccount = new Account
            {
                TenantId = currentTenantId,
                CompanyId = companyId,
                AccountCode = "5120-FX",
                AccountName = "Exchange Gain/Loss",
                Type = AccountType.Expense,
                RootType = AccountRootType.Expense,
                IsGroup = false,
                CurrencyId = null // Base currency
            };
            
            await context.Accounts.AddAsync(fxAccount, cancellationToken);
            
            // Link it to the company defaults
            var company = await context.Companies.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == companyId, cancellationToken);
            if (company != null)
            {
                company.DefaultExchangeGainLossAccountCode = "5120-FX";
                company.DefaultExchangeGainLossAccountId = fxAccount.Id;
            }
            
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
