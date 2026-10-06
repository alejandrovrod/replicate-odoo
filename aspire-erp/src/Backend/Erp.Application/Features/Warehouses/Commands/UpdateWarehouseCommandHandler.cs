using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Erp.Application.Features.Warehouses.Commands;

public sealed class UpdateWarehouseCommandHandler : ICommandHandler<UpdateWarehouseCommand, Result<WarehouseDto>>
{
    private readonly IWarehouseRepository _warehouses;
    private readonly IAccountRepository _accounts;

    public UpdateWarehouseCommandHandler(IWarehouseRepository warehouses, IAccountRepository accounts)
    {
        _warehouses = warehouses;
        _accounts = accounts;
    }

    public async Task<Result<WarehouseDto>> HandleAsync(UpdateWarehouseCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var warehouse = await _warehouses.GetByIdAsync(command.Id, cancellationToken);
            if (warehouse == null)
            {
                throw new StockValidationException(
                    StockErrorCodes.WarehouseNotFound,
                    $"Warehouse '{command.Id}' was not found in this tenant.");
            }

            if (warehouse.RowVersion is null || !warehouse.RowVersion.AsSpan().SequenceEqual(command.RowVersion))
            {
                throw new ConcurrencyConflictException(nameof(Warehouse), warehouse.Id);
            }

            WarehouseValidator.EnsureValidFields(
                command.CompanyId,
                command.Code,
                command.Name,
                command.AccountId);

            if (await _accounts.GetByIdAsync(command.AccountId, cancellationToken) is null)
            {
                throw new StockValidationException(
                    StockErrorCodes.MissingStockAccount,
                    $"The linked stock account '{command.AccountId}' does not exist in this tenant.");
            }

            IReadOnlyList<Warehouse> ancestors = Array.Empty<Warehouse>();

            if (command.ParentWarehouseId is { } parentId)
            {
                ancestors = await _warehouses.GetByIdWithAncestorsAsync(parentId, cancellationToken);

                if (ancestors.Count == 0)
                {
                    throw new StockValidationException(
                        StockErrorCodes.ParentWarehouseNotFound,
                        $"Parent warehouse '{parentId}' was not found in this tenant.");
                }
            }

            string newCode = command.Code.Trim();
            if (warehouse.WarehouseCode != newCode)
            {
                if (await _warehouses.ExistsByCodeAsync(command.CompanyId, newCode, cancellationToken))
                {
                    throw new StockValidationException(
                        StockErrorCodes.DuplicateWarehouseCode,
                        $"Warehouse code '{newCode}' already exists in this company.");
                }
            }

            warehouse.CompanyId = command.CompanyId;
            warehouse.WarehouseCode = newCode;
            warehouse.WarehouseName = command.Name.Trim();
            warehouse.ParentWarehouseId = command.ParentWarehouseId;
            warehouse.AccountId = command.AccountId;
            warehouse.IsGroup = command.IsGroup;
            warehouse.IsActive = command.IsActive;

            if (ancestors.Count > 0)
            {
                WarehouseValidator.EnsureValidParent(warehouse, ancestors[0]);

                var ancestorChain = new List<Guid>(ancestors.Count);
                foreach (var ancestor in ancestors)
                {
                    ancestorChain.Add(ancestor.Id);
                }

                WarehouseValidator.EnsureNoCycle(warehouse.Id, ancestorChain);
            }

            await _warehouses.UpdateAsync(warehouse, cancellationToken);

            return Result<WarehouseDto>.Success(WarehouseDto.From(warehouse));
        }
        catch (StockValidationException ex)
        {
            return Result<WarehouseDto>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Result<WarehouseDto>.Failure(ex.Code, ex.Message);
        }
    }
}
