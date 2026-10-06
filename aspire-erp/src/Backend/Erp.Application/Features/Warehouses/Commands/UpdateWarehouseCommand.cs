using Erp.Application.Common;
using Erp.Application.DTOs;
using System;

namespace Erp.Application.Features.Warehouses.Commands;

/// <summary>
/// Updates an existing warehouse node inside a company's warehouse tree.
/// Requires the exact RowVersion for optimistic concurrency control to prevent lost updates.
/// </summary>
public sealed record UpdateWarehouseCommand(
    Guid Id,
    Guid CompanyId,
    string Code,
    string Name,
    Guid AccountId,
    Guid? ParentWarehouseId,
    bool IsGroup,
    bool IsActive,
    byte[] RowVersion) : ICommand<Result<WarehouseDto>>;
