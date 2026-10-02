using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;

namespace Erp.Application.Features.Items.Commands;

/// <summary>
/// Creates one SKU (Task 3.1). Duplicate SKUs are rejected per TENANT (DoD 3.1) and reported as a
/// 409 domain failure through <see cref="Result{T}"/> - the same pattern as CreateAccountCommand.
/// </summary>
public sealed record CreateItemCommand(
    string Code,
    string Name,
    ValuationMethod ValuationMethod,
    Guid BaseUOMId,
    Guid? IncomeAccountId = null,
    Guid? ExpenseAccountId = null,
    bool IsActive = true) : ICommand<Result<ItemDto>>;
