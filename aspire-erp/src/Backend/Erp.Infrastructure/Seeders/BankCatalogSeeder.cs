using Erp.Domain.Entities;
using Erp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Seeders;

/// <summary>
/// Seeds the offline master bank catalog (bank-catalog spec §5, source: Data/Seed/banks_MX.json,
/// mirrored below so the seeder needs no file I/O at boot). Idempotent per
/// (TenantId, CountryCode, Code): re-runs skip existing rows (spec BNK-G4), converging on the
/// <c>UQ_Banks_Tenant_Country_Code</c> unique index instead of crashing parallel boots.
/// </summary>
/// <remarks>
/// Mirrors <see cref="ReceivableAccountSeeder"/>: static, context-based, explicit tenant scoping
/// with <c>IgnoreQueryFilters</c> (it runs outside any request tenant context, inside a
/// per-tenant scope pinned by Program.cs).
/// </remarks>
public static class BankCatalogSeeder
{
    private sealed record SeedBank(string CountryCode, string Code, string Name, string? SwiftBic);

    // Mirror of Data/Seed/banks_MX.json (spec §5): 3-digit Banxico/SPEI clave + official name;
    // swiftBic only when official and known, else null.
    private static readonly SeedBank[] MxBanks =
    [
        new("MX", "002", "Banco Nacional de México (Banamex)", "BNMXMXMM"),
        new("MX", "012", "BBVA México", "BCMRMXMM"),
        new("MX", "014", "Banco Santander México", "BMSXMXMM"),
        new("MX", "021", "HSBC México", "BIMEMXMM"),
        new("MX", "030", "Banco del Bajío", null),
        new("MX", "044", "Scotiabank México", null),
        new("MX", "058", "Banco Regional (Banregio)", null),
        new("MX", "062", "Banca Afirme", null),
        new("MX", "072", "Banco Mercantil del Norte (Banorte)", "MENOMXMT"),
        new("MX", "127", "Banco Azteca", null),
    ];

    public static async Task SeedAsync(AppDbContext context, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var existing = await context.Banks
            .IgnoreQueryFilters()
            .Where(b => b.TenantId == tenantId && b.CountryCode == "MX")
            .Select(b => b.Code)
            .ToListAsync(cancellationToken);

        var known = new HashSet<string>(existing, StringComparer.Ordinal);
        var missing = MxBanks.Where(b => !known.Contains(b.Code)).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        foreach (var seed in missing)
        {
            await context.Banks.AddAsync(new Bank
            {
                TenantId = tenantId,
                CountryCode = seed.CountryCode,
                Code = seed.Code,
                Name = seed.Name,
                SwiftBic = seed.SwiftBic,
                IsActive = true,
            }, cancellationToken);
        }

        // Converge on concurrent boots (ReceivableAccountSeeder precedent): parallel hosts may
        // all pass the existence check above for the same tenant; a 2601/2627 proves another
        // boot won and is not fatal - anything else rethrows loudly.
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            foreach (var entry in context.ChangeTracker.Entries<Bank>())
            {
                entry.State = EntityState.Detached;
            }
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is Microsoft.Data.SqlClient.SqlException sql
            && (sql.Number == 2601 || sql.Number == 2627);
}
