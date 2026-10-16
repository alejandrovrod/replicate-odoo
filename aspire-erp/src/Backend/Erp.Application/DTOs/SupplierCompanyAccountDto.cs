using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>
/// One per-company account mapping of a Supplier (module buying-suppliers, spec §1 gap D2),
/// serialized inside <see cref="SupplierDto.Accounts"/>.
/// </summary>
public sealed record SupplierCompanyAccountDto(
    Guid Id,
    Guid CompanyId,
    Guid PayableAccountId,
    Guid? AdvanceAccountId)
{
    public static SupplierCompanyAccountDto From(SupplierCompanyAccount account) =>
        new(account.Id, account.CompanyId, account.PayableAccountId, account.AdvanceAccountId);
}
