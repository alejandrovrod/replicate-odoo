using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;

namespace Erp.Application.Features.Items.Commands;

public sealed record UpdateItemCommand(
    Guid Id,
    string Code,
    string Name,
    ValuationMethod ValuationMethod,
    Guid BaseUOMId,
    Guid? IncomeAccountId,
    Guid? ExpenseAccountId,
    bool IsActive,
    byte[] RowVersion) : ICommand<Result<ItemDto>>;
