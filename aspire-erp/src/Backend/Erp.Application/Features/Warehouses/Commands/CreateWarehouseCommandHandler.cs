using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Warehouses.Commands;

/// <summary>
/// Executes <see cref="CreateWarehouseCommand"/>: field validation + tree rules through the pure
/// <see cref="WarehouseValidator"/> (parent chain loaded through IWarehouseRepository for cycle
/// prevention - the same decision C5 pattern as accounts) + duplicate code per company.
/// </summary>
public sealed class CreateWarehouseCommandHandler : ICommandHandler<CreateWarehouseCommand, Result<WarehouseDto>>
{
    private readonly IWarehouseRepository _warehouses;
    private readonly IAccountRepository _accounts;

    public CreateWarehouseCommandHandler(IWarehouseRepository warehouses, IAccountRepository accounts)
    {
        _warehouses = warehouses;
        _accounts = accounts;
    }

    public async Task<Result<WarehouseDto>> HandleAsync(CreateWarehouseCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            WarehouseValidator.EnsureValidFields(
                command.CompanyId,
                command.Code,
                command.Name,
                command.StockAccountId);

            // The linked stock account must exist (FK sanity -> avoids a 500 from the database).
            if (await _accounts.GetByIdAsync(command.StockAccountId, cancellationToken) is null)
            {
                throw new StockValidationException(
                    StockErrorCodes.MissingStockAccount,
                    $"The linked stock account '{command.StockAccountId}' does not exist in this tenant.");
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

            var warehouse = new Warehouse
            {
                Id = Guid.NewGuid(),
                CompanyId = command.CompanyId,
                Code = command.Code.Trim(),
                Name = command.Name.Trim(),
                ParentWarehouseId = command.ParentWarehouseId,
                StockAccountId = command.StockAccountId,
                IsGroup = command.IsGroup,
                IsActive = command.IsActive,

                // TenantId is intentionally NOT set: AppDbContext stamps it on insert (II.4).
            };

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

            if (await _warehouses.ExistsByCodeAsync(warehouse.CompanyId, warehouse.Code, cancellationToken))
            {
                throw new StockValidationException(
                    StockErrorCodes.DuplicateWarehouseCode,
                    $"Warehouse code '{warehouse.Code}' already exists in this company.");
            }

            await _warehouses.AddAsync(warehouse, cancellationToken);

            return Result<WarehouseDto>.Success(WarehouseDto.From(warehouse));
        }
        catch (StockValidationException ex)
        {
            return Result<WarehouseDto>.Failure(ex.Code, ex.Message);
        }
    }
}
