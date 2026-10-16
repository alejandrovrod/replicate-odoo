using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Buying.Commands;

/// <summary>
/// Executes <see cref="CreatePurchaseOrderCommand"/>: company/supplier/item resolution, pure
/// Domain line validation and the gapless PO voucher - all inside ONE transaction, because the
/// number is assigned by a SELECT MAX ... WITH (UPDLOCK, HOLDLOCK) that requires it
/// (Constitution III.4). The order is created in Draft (Task 4.1).
/// </summary>
public sealed class CreatePurchaseOrderCommandHandler
    : ICommandHandler<CreatePurchaseOrderCommand, Result<PurchaseOrderDto>>
{
    private const string VoucherPrefix = "PO";

    private readonly ICompanyRepository _companies;
    private readonly ISupplierRepository _suppliers;
    private readonly IItemRepository _items;
    private readonly IPurchaseRepository _purchases;

    public CreatePurchaseOrderCommandHandler(
        ICompanyRepository companies,
        ISupplierRepository suppliers,
        IItemRepository items,
        IPurchaseRepository purchases)
    {
        _companies = companies;
        _suppliers = suppliers;
        _items = items;
        _purchases = purchases;
    }

    public async Task<Result<PurchaseOrderDto>> HandleAsync(
        CreatePurchaseOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            PurchaseValidator.EnsureHasLines(command.Items);

            var company = await _companies.GetByIdAsync(command.CompanyId, cancellationToken)
                ?? throw new PurchaseValidationException(
                    PurchaseErrorCodes.CompanyNotFound,
                    $"Company '{command.CompanyId}' was not found in this tenant.");

            var supplier = await _suppliers.GetByIdAsync(command.SupplierId, cancellationToken)
                ?? throw new PurchaseValidationException(
                    PurchaseErrorCodes.SupplierNotFound,
                    $"Supplier '{command.SupplierId}' was not found in this tenant.");

            if (!supplier.IsActive)
            {
                throw new PurchaseValidationException(
                    PurchaseErrorCodes.SupplierInactive,
                    $"Supplier '{supplier.Code}' is inactive and cannot receive new purchase orders.");
            }

            // Governance guard (module buying-suppliers, spec §1 gap D4): only a FULL hold
            // (HoldType.All) blocks creating a purchase order - an "Invoices"-scoped hold still
            // allows ordering, matching ERPNext's on_hold semantics.
            if (supplier.IsBlockedFor(SupplierHoldType.All, command.TransactionDate))
            {
                throw new PurchaseValidationException(
                    PurchaseErrorCodes.SupplierOnHold,
                    $"Supplier '{supplier.Code}' is on hold for all documents and cannot receive new purchase orders.");
            }

            var items = await LoadItemsAsync(command.Items!, cancellationToken);

            // Calculate totals
            decimal netTotal = 0;
            foreach (var item in command.Items!)
            {
                netTotal += item.Quantity * item.Rate;
            }

            // One transaction for number + insert: a rollback releases the lock and consumes no number.
            var order = await _purchases.ExecuteInTransactionAsync(async token =>
            {
                var entity = new PurchaseOrder
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    SupplierId = supplier.Id,
                    Status = PurchaseOrderStatus.Draft,
                    TransactionDate = command.TransactionDate,
                    ScheduleDate = command.ScheduleDate,
                    OrderNumber = await _purchases.NextOrderVoucherNumberAsync(
                        company.Id, VoucherPrefix, command.TransactionDate.Year, token),
                    NetTotal = netTotal,
                    TaxTotal = 0,
                    GrandTotal = netTotal,
                    CreatedAt = DateTimeOffset.UtcNow,
                    Items = BuildItems(command.Items!, items),
                };

                await _purchases.AddOrderAsync(entity, token);
                return entity;
            }, cancellationToken);

            return Result<PurchaseOrderDto>.Success(PurchaseOrderDto.Build(order, supplier, items));
        }
        catch (PurchaseValidationException ex)
        {
            return Result<PurchaseOrderDto>.Failure(ex.Code, ex.Message);
        }
        catch (StockValidationException ex)
        {
            return Result<PurchaseOrderDto>.Failure(ex.Code, ex.Message);
        }
    }

    private async Task<Dictionary<Guid, Item>> LoadItemsAsync(
        IReadOnlyList<CreatePurchaseOrderItem> items,
        CancellationToken cancellationToken)
    {
        var ids = new List<Guid>(items.Count);
        foreach (var item in items)
        {
            if (item.ItemId == Guid.Empty)
            {
                throw new StockValidationException(
                    StockErrorCodes.ItemNotFound, "A line references an empty ItemId.");
            }

            if (!ids.Contains(item.ItemId))
            {
                ids.Add(item.ItemId);
            }
        }

        var found = await _items.GetByIdsAsync(ids, cancellationToken);
        var byId = new Dictionary<Guid, Item>(found.Count);
        foreach (var item in found)
        {
            byId[item.Id] = item;
        }

        foreach (var id in ids)
        {
            if (!byId.ContainsKey(id))
            {
                throw new StockValidationException(
                    StockErrorCodes.ItemNotFound,
                    $"Item '{id}' was not found in this tenant.");
            }
        }

        return byId;
    }

    private static List<PurchaseOrderItem> BuildItems(
        IReadOnlyList<CreatePurchaseOrderItem> items,
        IReadOnlyDictionary<Guid, Item> itemMap)
    {
        var result = new List<PurchaseOrderItem>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            PurchaseValidator.EnsureValidLine(item.Quantity, item.Rate);
            _ = itemMap[item.ItemId];

            result.Add(new PurchaseOrderItem
            {
                Id = Guid.NewGuid(),
                ItemId = item.ItemId,
                Quantity = item.Quantity,
                Rate = item.Rate,
                Amount = item.Quantity * item.Rate,
                LineNumber = i + 1,
            });
        }

        return result;
    }
}
