using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// Processing outcome of a statement import batch (plan.md §1 <c>ImportStatus</c>).
/// </summary>
public enum BankImportStatus
{
    Processed,
    Failed,
}

/// <summary>
/// Batch header for one uploaded statement file (plan.md §1 DDL).
/// <see cref="ImportedCount"/> / <see cref="DuplicateCount"/> are REQUIRED by scenario BN-05
/// (the import summary: total imported vs duplicate skipped) - a justified deviation from the
/// plan DDL, which tracks only the raw row count.
/// </summary>
public sealed class BankStatementImport : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public Guid BankAccountId { get; set; }

    public BankAccount? BankAccount { get; set; }

    /// <summary>Uploaded file name (max 255 chars, plan.md §1).</summary>
    public string FileName { get; set; } = string.Empty;

    public DateTimeOffset ImportDate { get; set; }

    /// <summary>Raw rows parsed from the file (plan.md §1 TotalTransactionsCount).</summary>
    public int TotalTransactionsCount { get; set; }

    /// <summary>Rows persisted as staging transactions (BN-05 summary).</summary>
    public int ImportedCount { get; set; }

    /// <summary>Rows skipped as already-imported FITIDs (BN-05 summary).</summary>
    public int DuplicateCount { get; set; }

    public BankImportStatus ImportStatus { get; set; } = BankImportStatus.Processed;
}
