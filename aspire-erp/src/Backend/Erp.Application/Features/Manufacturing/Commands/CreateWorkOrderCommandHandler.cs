using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Manufacturing.Commands;

/// <summary>
/// Executes <see cref="CreateWorkOrderCommand"/>: master resolution, pure Domain field guards
/// and the gapless WO voucher - all inside ONE transaction, because the number is assigned by
/// a SELECT MAX ... WITH (UPDLOCK, HOLDLOCK) that requires it (Constitution III.4). The order
/// is created in Draft (Task 9.3); submission is a separate <see cref="SubmitWorkOrderCommand"/>.
/// </summary>
public sealed class CreateWorkOrderCommandHandler
    : ICommandHandler<CreateWorkOrderCommand, Result<WorkOrderDto>>
{
    private const string VoucherPrefix = "WO";

    private readonly ICompanyRepository _companies;
    private readonly IItemRepository _items;
    private readonly IWarehouseRepository _warehouses;
    private readonly IManufacturingRepository _manufacturing;

    public CreateWorkOrderCommandHandler(
        ICompanyRepository companies,
        IItemRepository items,
        IWarehouseRepository warehouses,
        IManufacturingRepository manufacturing)
    {
        _companies = companies;
        _items = items;
        _warehouses = warehouses;
        _manufacturing = manufacturing;
    }

    public async Task<Result<WorkOrderDto>> HandleAsync(
        CreateWorkOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            WorkOrderValidator.EnsureValidQuantity(command.QuantityToProduce);
            WorkOrderValidator.EnsureValidDates(command.PlannedStartDate, command.PlannedEndDate);
            WorkOrderValidator.EnsureValidWarehouses(
                command.SourceWarehouseId, command.WipWarehouseId, command.TargetWarehouseId);

            var company = await _companies.GetByIdAsync(command.CompanyId, cancellationToken)
                ?? throw new ManufacturingValidationException(
                    ManufacturingErrorCodes.WorkOrderNotFound,
                    $"Company '{command.CompanyId}' was not found in this tenant.");

            var item = await _items.GetByIdAsync(command.ProductionItemId, cancellationToken)
                ?? throw new ManufacturingValidationException(
                    ManufacturingErrorCodes.BomNotFound,
                    $"Production item '{command.ProductionItemId}' was not found in this tenant.");

            var bom = await _manufacturing.GetBomByIdAsync(command.BomId, cancellationToken)
                ?? throw new ManufacturingValidationException(
                    ManufacturingErrorCodes.BomNotFound,
                    $"BOM '{command.BomId}' was not found in this tenant.");

            if (bom.ItemId != item.Id)
            {
                throw new ManufacturingValidationException(
                    ManufacturingErrorCodes.BomNotFound,
                    $"BOM '{bom.BomNumber}' produces item '{bom.ItemId}' and cannot authorize production of item '{item.Id}'.");
            }

            await RequireCompanyWarehouseAsync(command.SourceWarehouseId, company.Id, "source (Stores)", cancellationToken);
            await RequireCompanyWarehouseAsync(command.WipWarehouseId, company.Id, "WIP transit", cancellationToken);
            await RequireCompanyWarehouseAsync(command.TargetWarehouseId, company.Id, "target (finished goods)", cancellationToken);

            // One transaction for number + insert: a rollback releases the lock and consumes no number.
            var order = await _manufacturing.ExecuteInTransactionAsync(async token =>
            {
                var entity = new WorkOrder
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    OrderNumber = await _manufacturing.NextWorkOrderNumberAsync(
                        company.Id, VoucherPrefix, command.PlannedStartDate.Year, token),
                    ProductionItemId = item.Id,
                    BomId = bom.Id,
                    QuantityToProduce = command.QuantityToProduce,
                    ProducedQuantity = 0m,
                    Status = WorkOrderStatus.Draft,
                    SourceWarehouseId = command.SourceWarehouseId,
                    WipWarehouseId = command.WipWarehouseId,
                    TargetWarehouseId = command.TargetWarehouseId,
                    PlannedStartDate = command.PlannedStartDate,
                    PlannedEndDate = command.PlannedEndDate,
                    CreatedAt = DateTimeOffset.UtcNow,
                };

                await _manufacturing.AddWorkOrderAsync(entity, token);
                return entity;
            }, cancellationToken);

            return Result<WorkOrderDto>.Success(WorkOrderDto.Build(order));
        }
        catch (ManufacturingValidationException ex)
        {
            return Result<WorkOrderDto>.Failure(ex.Code, ex.Message);
        }
    }

    private async Task RequireCompanyWarehouseAsync(
        Guid warehouseId, Guid companyId, string role, CancellationToken cancellationToken)
    {
        var warehouse = await _warehouses.GetByIdAsync(warehouseId, cancellationToken)
            ?? throw new ManufacturingValidationException(
                ManufacturingErrorCodes.InvalidWorkOrderWarehouses,
                $"The {role} warehouse '{warehouseId}' was not found in this tenant.");

        if (warehouse.CompanyId != companyId)
        {
            throw new ManufacturingValidationException(
                ManufacturingErrorCodes.InvalidWorkOrderWarehouses,
                $"The {role} warehouse '{warehouse.WarehouseCode}' does not belong to company '{companyId}'.");
        }
    }
}
