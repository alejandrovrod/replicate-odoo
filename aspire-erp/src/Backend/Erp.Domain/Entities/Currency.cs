namespace Erp.Domain.Entities;

/// <summary>
/// An ISO 4217 currency of the monetary catalog (RM-09 master data).
/// </summary>
/// <remarks>
/// GLOBAL (shared) master: unlike company-owned masters (<see cref="Account"/>, <see cref="Customer"/>),
/// ISO currencies are universal, so this entity carries NO <c>TenantId</c> and implements no
/// tenant contract - AppDbContext only applies its global query filter to <c>ITenantEntity</c>
/// types, and a missing TenantId is explicitly allowed for shared catalogs. Codes are unique
/// GLOBALLY (IX_Currency_Code). Deliberately NOT system-versioned (Supplier/Item precedent):
/// code/symbol/fraction renames are rare and need no history table.
/// Validation rules live in <see cref="CurrencyValidator"/> (pure Domain).
/// </remarks>
public class Currency
{
    public Guid Id { get; set; }

    /// <summary>ISO 4217 code, e.g. "USD" (max 3 chars, unique).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Display symbol, e.g. "$" (max 10 chars).</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Fraction (subunit) name, e.g. "Cent" (max 50 chars).</summary>
    public string FractionName { get; set; } = string.Empty;

    /// <summary>Inactive currencies cannot be linked from new master records.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optimistic concurrency token (SQL Server <c>rowversion</c>): EF puts the original value
    /// in the UPDATE ... WHERE clause, so a concurrent change between load and save throws
    /// <c>DbUpdateConcurrencyException</c> instead of silently winning.
    /// Store-generated: never set from code.
    /// </summary>
    public byte[] RowVersion { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
}
