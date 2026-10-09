using Erp.Application.Common;
using Erp.Domain.Entities;
using Erp.Infrastructure.Data;
using Erp.Infrastructure.Seeders;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Api.IntegrationTests;

/// <summary>
/// Phase 8 hardening evidence for <see cref="ReceivableAccountSeeder"/> against the LIVE dev
/// SQL container: a company with an empty chart of accounts gets exactly one
/// "1120 - Deudores por Ventas" leaf typed Receivable plus the company default code, repeat
/// runs are no-ops, and concurrent runs converge on the unique index instead of crashing.
/// </summary>
/// <remarks>
/// <para><b>No HTTP, no fixture, no ledger writes:</b> like <c>GLEntryLedgerGuardTests</c> this
/// class talks to the container directly through tenant-pinned contexts, so it serializes
/// with nothing and runs in parallel with every suite. Fresh tenant ids per test keep all
/// rows invisible to sibling suites.</para>
///
/// <para><b>Regression:</b> the seeder used to insert through a tenantless scope, so the
/// fail-closed tenant guard rejected the very row it exists to create (Phase 8 finding H1).
/// These tests pin the fixed contract: explicit tenant scoping + convergence.</para>
/// </remarks>
public class ReceivableAccountSeederTests : IAsyncLifetime
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private Guid _companyId;

    public async Task InitializeAsync()
    {
        _companyId = Guid.NewGuid();

        await using var context = CreateContext(_tenantId);
        context.Tenants.Add(new Tenant
        {
            Id = _tenantId,
            Name = "T8C Tenant",
            Code = $"T8C-{_tenantId:N}"[..16],
            CreatedAt = DateTimeOffset.UtcNow,
        });
        context.Companies.Add(new Company
        {
            Id = _companyId,
            Name = "T8C Company",
            TaxId = "T8C-TAX",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync();
    }

    /// <summary>Empty COA → one Receivable 1120 leaf + company default code; second run changes nothing.</summary>
    [Fact]
    public async Task SeedAsync_EmptyChart_CreatesReceivableLeafAndDefault_AndIsIdempotent()
    {
        await using (var context = CreateContext(_tenantId))
        {
            await ReceivableAccountSeeder.SeedAsync(context, _companyId, _tenantId);
        }

        await using (var verifier = CreateContext(_tenantId))
        {
            var leaves = await verifier.Accounts
                .Where(a => a.CompanyId == _companyId && a.AccountCode == "1120")
                .ToListAsync();
            var leaf = Assert.Single(leaves);
            Assert.Equal("Deudores por Ventas", leaf.AccountName);
            Assert.Equal(AccountType.Receivable, leaf.Type);
            Assert.False(leaf.IsGroup);
            Assert.True(leaf.IsActive);

            var company = await verifier.Companies.FirstAsync(c => c.Id == _companyId);
            Assert.Equal("1120", company.DefaultReceivableAccountCode);
        }

        // Second run: still exactly one row, same values (idempotent, no duplicates).
        await using (var rerun = CreateContext(_tenantId))
        {
            await ReceivableAccountSeeder.SeedAsync(rerun, _companyId, _tenantId);
        }

        await using (var verifier = CreateContext(_tenantId))
        {
            Assert.Equal(1, await verifier.Accounts.CountAsync(a => a.CompanyId == _companyId));
        }
    }

    /// <summary>Parallel boots race on the same company: exactly one 1120 survives, nobody throws.</summary>
    [Fact]
    public async Task SeedAsync_ConcurrentRuns_ConvergeOnSingleRow()
    {
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
        {
            await using var context = CreateContext(_tenantId);
            await ReceivableAccountSeeder.SeedAsync(context, _companyId, _tenantId);
        })));

        await using var verifier = CreateContext(_tenantId);
        Assert.Equal(1, await verifier.Accounts.CountAsync(a => a.CompanyId == _companyId));
    }

    public async Task DisposeAsync()
    {
        await using var context = CreateContext(_tenantId);
        context.Accounts.RemoveRange(context.Accounts);
        context.Companies.RemoveRange(context.Companies);
        await context.SaveChangesAsync();

        await using var root = CreateContext(_tenantId);
        var tenant = await root.Tenants.FirstOrDefaultAsync(t => t.Id == _tenantId);
        if (tenant is not null)
        {
            root.Tenants.Remove(tenant);
            await root.SaveChangesAsync();
        }
    }

    private static AppDbContext CreateContext(Guid tenantId) =>
        new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(ErpApiFactory.DevConnectionString)
                .Options,
            new StubTenantProvider(tenantId));

    private sealed class StubTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;

        public StubTenantProvider(Guid tenantId) => _tenantId = tenantId;

        public Guid GetCurrentTenantId() => _tenantId;

        public bool HasTenant() => _tenantId != Guid.Empty;

        public void SetCurrentTenantId(Guid tenantId) => throw new NotSupportedException();
    }
}
