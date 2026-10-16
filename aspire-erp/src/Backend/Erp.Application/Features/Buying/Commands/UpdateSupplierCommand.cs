using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;

namespace Erp.Application.Features.Buying.Commands;

/// <summary>
/// Updates an existing Supplier (Task 4.1, extended with group, multi-company accounts and the
/// governance flags: IsFrozen, hold status and release date - module buying-suppliers spec §1).
/// </summary>
public sealed record UpdateSupplierCommand(
    Guid Id,
    string Code,
    string Name,
    string? TaxId,
    int PaymentTermsDays,
    Guid? PayableAccountId,
    bool IsActive,
    byte[] RowVersion,
    string? CustomData = null,
    Guid? SupplierGroupId = null,
    Guid? PaymentTermsTemplateId = null,
    Guid? DefaultPriceListId = null,
    bool IsFrozen = false,
    bool OnHold = false,
    SupplierHoldType HoldType = SupplierHoldType.None,
    DateOnly? ReleaseDate = null,
    IReadOnlyList<SupplierCompanyAccountInput>? CompanyAccounts = null) : ICommand<Result<SupplierDto>>;
