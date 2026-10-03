using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Warehouses.Commands;

/// <summary>
/// Creates one warehouse node inside a company's warehouse tree (Task 3.1: "Warehouses enforce
/// tree structure"). Duplicate codes are rejected per company; tree violations (self-parent,
/// cross-company parent, non-group parent, cycles) surface as 400 domain failures.
/// </summary>
public sealed record CreateWarehouseCommand(
    Guid CompanyId,
    string Code,
    string Name,
    Guid AccountId,
    Guid? ParentWarehouseId = null,
    bool IsGroup = false,
    bool IsActive = true) : ICommand<Result<WarehouseDto>>;

