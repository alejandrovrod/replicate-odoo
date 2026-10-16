namespace Erp.Domain.Entities;

/// <summary>
/// Multi-company accounting account mapping for a <see cref="Supplier"/> (spec §1 gap D2, ERPNext
/// "Multi-Company Party Account"): in a multi-company tenant one vendor holds a DIFFERENT payable
/// account (and optional advance account) per legal company that buys from it.
/// </summary>
/// <remarks>
/// Child of the <see cref="Supplier"/> aggregate: no <c>TenantId</c> of its own, it inherits the
/// tenant through its parent. Unique per (<c>SupplierId</c>, <c>CompanyId</c>) at the database.
/// </remarks>
public class SupplierCompanyAccount
{
    public Guid Id { get; set; }

    public Guid SupplierId { get; set; }

    public Supplier? Supplier { get; set; }

    /// <summary>Legal company (of the tenant) this mapping resolves for.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>Payable (Accounts Payable) account used when this company bills the supplier.</summary>
    public Guid PayableAccountId { get; set; }

    /// <summary>Optional advance/prepayment account for this company.</summary>
    public Guid? AdvanceAccountId { get; set; }

    /// <summary>Refreshes the mapping in place (spec <c>SupplierCompanyAccount.Update</c>).</summary>
    public void Update(Guid payableAccountId, Guid? advanceAccountId = null)
    {
        PayableAccountId = payableAccountId;
        AdvanceAccountId = advanceAccountId;
    }
}
