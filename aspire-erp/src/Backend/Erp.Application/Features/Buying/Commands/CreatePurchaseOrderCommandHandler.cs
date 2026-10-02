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
            PurchaseValidator.EnsureHasLines(command.Lines);

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

            var postingDate = command.PostingDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var items = await LoadItemsAsync(command.Lines!, cancellationToken);

            // One transaction for number + insert: a rollback releases the lock and consumes no number.
            var order = await _purchases.ExecuteInTransactionAsync(async token =>
            {
                var entity = new PurchaseOrder
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    SupplierId = supplier.Id,
                    Status = PurchaseOrderStatus.Draft,
                    PostingDate = postingDate,
                    VoucherNo = await _purchases.NextOrderVoucherNumberAsync(
                        company.Id, VoucherPrefix, postingDate.Year, token),
                    CreatedAt = DateTimeOffset.UtcNow,
                    Lines = BuildLines(command.Lines!, items),
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
        IReadOnlyList<CreatePurchaseOrderLine> lines,
        CancellationToken cancellationToken)
    {
        var ids = new List<Guid>(lines.Count);
        foreach (var line in lines)
        {
            if (line.ItemId == Guid.Empty)
            {
                throw new StockValidationException(
                    StockErrorCodes.ItemNotFound, "A line references an empty ItemId.");
            }

            if (!ids.Contains(line.ItemId))
            {
                ids.Add(line.ItemId);
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

    private static List<PurchaseOrderLine> BuildLines(
        IReadOnlyList<CreatePurchaseOrderLine> lines,
        IReadOnlyDictionary<Guid, Item> items)
    {
        var result = new List<PurchaseOrderLine>(lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            PurchaseValidator.EnsureValidLine(line.Qty, line.Rate);
            _ = items[line.ItemId];

            result.Add(new PurchaseOrderLine
            {
                Id = Guid.NewGuid(),
                ItemId = line.ItemId,
                Qty = line.Qty,
                Rate = line.Rate,
                LineNumber = i + 1,
            });
        }

        return result;
    }
}
