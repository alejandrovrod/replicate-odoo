using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// Master bank catalog row (bank-catalog spec §3): one bank per (TenantId, CountryCode, Code),
/// 100% offline - no live external APIs. A <see cref="BankAccount"/> may reference it through
/// its optional <c>BankId</c>; the free-text <c>BankName</c> stays as fallback/display.
/// </summary>
/// <remarks>
/// Tenant-scoped: implements <c>ITenantEntity</c> (Constitution Article II.1). System-versioned
/// like BankAccount (Constitution Article IV.2, history table <c>BanksHistory</c>), so catalog
/// renames keep a full audit trail. No <c>RowVersion</c>: the temporal history is the
/// concurrency/audit story for this reference master (plan.md certified decision).
/// Validation rules live in <see cref="BankValidator"/> (pure Domain).
/// </remarks>
public sealed class Bank : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    /// <summary>ISO 3166-1 alpha-2 country, uppercase (e.g. "MX"). Max 2 chars.</summary>
    public string CountryCode { get; set; } = string.Empty;

    /// <summary>
    /// Bank key within the country (MX: 3-digit Banxico/SPEI key, e.g. "012"). Max 20 chars.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Official bank name (max 150 chars).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Official SWIFT/BIC when known (max 11 chars); null otherwise.</summary>
    public string? SwiftBic { get; set; }

    /// <summary>Inactive banks are hidden from autocomplete but keep history.</summary>
    public bool IsActive { get; set; } = true;
}
