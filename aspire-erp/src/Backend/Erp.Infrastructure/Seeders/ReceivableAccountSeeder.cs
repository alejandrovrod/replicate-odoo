using Erp.Domain.Entities;
using Erp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Seeders;

/// <summary>
/// Ensures every company owns a postable <see cref="AccountType.Receivable"/> account so the
/// Customer "Default Receivable Account" picker is never empty on new companies (Selling Fase 5).
/// Idempotent: safe to run on every application initialization.
/// </summary>
/// <remarks>
/// Mirrors <see cref="ExchangeGainLossSeeder"/>: static, context-based, explicit tenant/company
/// scoping with <c>IgnoreQueryFilters</c> (it runs outside any request tenant context).
/// Resolution order: reuse an existing Receivable leaf, else upgrade the legacy "1120" leaf,
/// else create "1120 - Deudores por Ventas" (attached under the "1000" Assets group when one
/// exists). In every case the company's <c>DefaultReceivableAccountCode</c> is backfilled when
/// empty, which is the code <c>SubmitSalesInvoiceCommandHandler</c> resolves at posting time.
/// </remarks>
public static class ReceivableAccountSeeder
{
    public const string ReceivableAccountCode = "1120";
    public const string ReceivableAccountName = "Deudores por Ventas";

    public static async Task SeedAsync(AppDbContext context, Guid companyId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var receivable = await context.Accounts
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId
                && a.CompanyId == companyId
                && a.Type == AccountType.Receivable
                && a.IsActive
                && !a.IsGroup)
            .OrderBy(a => a.AccountCode)
            .FirstOrDefaultAsync(cancellationToken);

        var company = await context.Companies
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == companyId, cancellationToken);

        if (receivable is not null)
        {
            if (company is not null && string.IsNullOrWhiteSpace(company.DefaultReceivableAccountCode))
            {
                company.DefaultReceivableAccountCode = receivable.AccountCode;
                await context.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        var legacy = await context.Accounts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                a => a.TenantId == tenantId && a.CompanyId == companyId && a.AccountCode == ReceivableAccountCode,
                cancellationToken);

        if (legacy is not null)
        {
            legacy.Type = AccountType.Receivable;
            if (company is not null && string.IsNullOrWhiteSpace(company.DefaultReceivableAccountCode))
            {
                company.DefaultReceivableAccountCode = legacy.AccountCode;
            }

            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        var assetsGroup = await context.Accounts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                a => a.TenantId == tenantId && a.CompanyId == companyId && a.AccountCode == "1000" && a.IsGroup,
                cancellationToken);

        var account = new Account
        {
            TenantId = tenantId,
            CompanyId = companyId,
            AccountCode = ReceivableAccountCode,
            AccountName = ReceivableAccountName,
            RootType = AccountRootType.Asset,
            Type = AccountType.Receivable,
            IsGroup = false,
            ParentAccountId = assetsGroup?.Id,
            IsActive = true,
        };

        await context.Accounts.AddAsync(account, cancellationToken);

        if (company is not null && string.IsNullOrWhiteSpace(company.DefaultReceivableAccountCode))
        {
            company.DefaultReceivableAccountCode = ReceivableAccountCode;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
