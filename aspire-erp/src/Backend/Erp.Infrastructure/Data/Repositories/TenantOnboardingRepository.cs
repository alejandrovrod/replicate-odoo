using Erp.Application.Common;
using Erp.Domain.Entities;
using Erp.Domain.Entities.Security;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Infrastructure.Seeders;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ITenantOnboardingRepository"/>.
/// </summary>
/// <remarks>
/// Registration is the only flow that writes tenant-scoped rows before a tenant exists in the
/// ambient context. It pins <see cref="ITenantProvider"/> to the freshly generated tenant id
/// inside the transaction, then restores the previous value in <c>finally</c> so the request
/// scope never leaks a half-created tenant.
/// </remarks>
public sealed class TenantOnboardingRepository : ITenantOnboardingRepository
{
    private const string SystemManagerRoleName = "System Manager";

    private readonly AppDbContext _context;
    private readonly ITenantProvider _tenantProvider;

    public TenantOnboardingRepository(AppDbContext context, ITenantProvider tenantProvider)
    {
        _context = context;
        _tenantProvider = tenantProvider;
    }

    public Task<bool> SubdomainExistsAsync(
        string subdomain,
        CancellationToken cancellationToken = default) =>
        _context.Tenants.AnyAsync(t => t.Subdomain == subdomain, cancellationToken);

    public Task<Tenant?> GetBySubdomainAsync(
        string subdomain,
        CancellationToken cancellationToken = default) =>
        _context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Subdomain == subdomain && t.IsActive, cancellationToken);

    public async Task<TenantRegistrationIds> RegisterAsync(
        TenantRegistrationData data,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        var previousTenantId = _tenantProvider.GetCurrentTenantId();
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            if (await SubdomainExistsAsync(data.Subdomain, cancellationToken))
            {
                throw new AuthValidationException(
                    TenantErrorCodes.SubdomainTaken,
                    $"The subdomain '{data.Subdomain}' is already in use.");
            }

            var now = DateTimeOffset.UtcNow;
            var tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Name = data.TenantName,
                Code = data.TenantCode,
                Subdomain = data.Subdomain,
                DefaultLanguage = TenantLanguage.EnsureValid(data.DefaultLanguage),
                IsActive = true,
                CreatedAt = now,
            };

            _context.Tenants.Add(tenant);

            // Fail-closed tenant guard: every Company/Account/Role/User insert below is stamped
            // with this id, so the provider must be pinned before SaveChanges runs.
            _tenantProvider.SetCurrentTenantId(tenant.Id);

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = data.CompanyName,
                TaxId = data.CompanyTaxId,
                CreatedAt = now,
            };

            _context.Companies.Add(company);

            var accounts = StandardChartOfAccounts.Build(
                tenant.Id,
                company.Id,
                tenant.DefaultLanguage);
            _context.Accounts.AddRange(accounts.Values);

            // Posting defaults are account CODES (decision D3), not FKs - except retained
            // earnings, which also stores the resolved id for period closing.
            company.StockReceivedAccountCode = "2120";
            company.CogsAccountCode = "5210";
            company.DefaultReceivableAccountCode = "1120";
            company.DefaultIncomeAccountCode = "4110";
            company.AccountsPayableAccountCode = "2110";
            company.InputTaxRecoverableAccountCode = "1130";
            company.PriceDifferenceAccountCode = "5120";
            company.PayrollPayableAccountCode = "2150";
            company.DefaultRetainedEarningsAccountId = accounts["3100"].Id;
            company.DefaultRetainedEarningsAccountCode = "3100";

            await _context.SaveChangesAsync(cancellationToken);

            // Starter catalogs: UOMs, root/main warehouses and fixed-asset categories. The COA
            // rows above are already persisted, so the category account links resolve cleanly.
            var starter = TenantStarterDataSeeder.Build(
                tenant.Id,
                company.Id,
                accounts,
                tenant.DefaultLanguage);

            _context.UOMs.AddRange(starter.UnitsOfMeasure);
            _context.Warehouses.AddRange(starter.Warehouses);
            _context.AssetCategories.AddRange(starter.AssetCategories);
            await _context.SaveChangesAsync(cancellationToken);

            // Fase R3 matrix: six roles + DocTypePermission rows for the new tenant.
            await SecurityRoleSeeder.SeedAsync(
                _context,
                tenant.Id,
                tenant.DefaultLanguage,
                cancellationToken);

            // The context was created before this request pinned the tenant (public endpoint), so
            // its cached model may still reference the empty-tenant provider instance. Read with
            // IgnoreQueryFilters and an explicit tenant predicate instead of relying on the global
            // filter here. Every insert below is still stamped by ITenantProvider.
            var systemManagerRoleId = await _context.Roles
                .IgnoreQueryFilters()
                .Where(r => r.TenantId == tenant.Id && r.Name == SystemManagerRoleName)
                .Select(r => r.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (systemManagerRoleId == Guid.Empty)
            {
                throw new InvalidOperationException(
                    "Tenant onboarding could not resolve the seeded System Manager role.");
            }

            var admin = new User
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Email = data.AdminEmail,
                FullName = data.AdminFullName,
                PasswordHash = data.AdminPasswordHash,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };

            _context.Users.Add(admin);
            _context.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                UserId = admin.Id,
                RoleId = systemManagerRoleId,
            });

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new TenantRegistrationIds(tenant.Id, company.Id, admin.Id);
        }
        catch (DbUpdateException ex) when (IsSubdomainUniqueViolation(ex))
        {
            throw new AuthValidationException(
                TenantErrorCodes.SubdomainTaken,
                $"The subdomain '{data.Subdomain}' is already in use.");
        }
        finally
        {
            // Do not leak the provisional tenant into the rest of the HTTP request (or into a
            // later call on the same scope when a test reuses the context). On rollback this is
            // also the fail-closed reset.
            _tenantProvider.SetCurrentTenantId(previousTenantId);
        }
    }

    private static bool IsSubdomainUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is Microsoft.Data.SqlClient.SqlException sql
        && (sql.Number == 2601 || sql.Number == 2627)
        && (sql.Message.Contains("UQ_Tenants_Subdomain", StringComparison.Ordinal)
            || sql.Message.Contains("IX_Tenant_Code", StringComparison.Ordinal));
}
