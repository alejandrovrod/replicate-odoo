using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;

namespace Erp.Application.Features.Buying.Commands;

/// <summary>
/// One per-company payable account mapping supplied when creating/updating a Supplier
/// (module buying-suppliers, spec §1 gap D2). <c>CompanyId</c> is unique inside the list.
/// </summary>
public sealed record SupplierCompanyAccountInput(
    Guid CompanyId,
    Guid PayableAccountId,
    Guid? AdvanceAccountId = null);

/// <summary>
/// Creates one Supplier (Task 4.1, extended with group, multi-company accounts and hold setup).
/// Duplicate codes are rejected per TENANT and reported as a 409 domain failure through
/// <see cref="Result{T}"/> - the same pattern as CreateItemCommand.
/// </summary>
public sealed record CreateSupplierCommand(
    string Code,
    string Name,
    string TaxId = "",
    Guid? DefaultPayableAccountId = null,
    Guid? CurrencyId = null,
    int PaymentTermsDays = 30,
    bool IsActive = true,
    string? CustomData = null,
    Guid? SupplierGroupId = null,
    Guid? PaymentTermsTemplateId = null,
    Guid? DefaultPriceListId = null,
    bool OnHold = false,
    SupplierHoldType HoldType = SupplierHoldType.None,
    DateOnly? ReleaseDate = null,
    IReadOnlyList<SupplierCompanyAccountInput>? CompanyAccounts = null) : ICommand<Result<SupplierDto>>;
