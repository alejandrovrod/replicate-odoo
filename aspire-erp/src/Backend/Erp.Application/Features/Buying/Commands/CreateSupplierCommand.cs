using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Buying.Commands;

/// <summary>
/// Creates one Supplier (Task 4.1). Duplicate codes are rejected per TENANT and reported as a
/// 409 domain failure through <see cref="Result{T}"/> - the same pattern as CreateItemCommand.
/// </summary>
public sealed record CreateSupplierCommand(
    string Code,
    string Name,
    string TaxId = "",
    Guid? DefaultPayableAccountId = null,
    Guid? CurrencyId = null,
    int PaymentTermsDays = 30,
    bool IsActive = true) : ICommand<Result<SupplierDto>>;
