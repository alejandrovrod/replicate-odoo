using Erp.Domain.Common;
using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Derived closing-line snapshot persisted at submit time (plan.md §2.3): the audit record of WHAT
/// was zeroed. Lines are COMPUTED from live GL balances, never hand-entered (spec §1).
/// </summary>
public class PeriodClosingVoucherLine : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation.</summary>
    public Guid TenantId { get; set; }

    public Guid VoucherId { get; set; }

    public PeriodClosingVoucher? Voucher { get; set; }

    /// <summary>P&amp;L leaf zeroed by this line (or the retained earnings mirror).</summary>
    public Guid AccountId { get; set; }

    public Account? Account { get; set; }

    public decimal Debit { get; set; }

    public decimal Credit { get; set; }
}
