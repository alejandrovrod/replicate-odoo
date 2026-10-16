using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.LandedCost.Queries;

/// <summary>Company landed cost vouchers, newest first (optional status filter).</summary>
public sealed record GetLandedCostVouchersQuery(
    Guid CompanyId,
    int Page = 1,
    int PageSize = 50,
    DocumentStatus? Status = null) : IQuery<PagedResult<LandedCostVoucherDto>>;

/// <summary>One voucher with references, charges and apportionment (or null).</summary>
public sealed record GetLandedCostVoucherByIdQuery(Guid CompanyId, Guid Id)
    : IQuery<LandedCostVoucherDto?>;

public sealed class GetLandedCostVouchersQueryHandler
    : IQueryHandler<GetLandedCostVouchersQuery, PagedResult<LandedCostVoucherDto>>
{
    private readonly ILandedCostVoucherRepository _vouchers;
    private readonly IPurchaseRepository _purchases;
    private readonly IItemRepository _items;

    public GetLandedCostVouchersQueryHandler(
        ILandedCostVoucherRepository vouchers,
        IPurchaseRepository purchases,
        IItemRepository items)
    {
        _vouchers = vouchers;
        _purchases = purchases;
        _items = items;
    }

    public async Task<PagedResult<LandedCostVoucherDto>> HandleAsync(
        GetLandedCostVouchersQuery query, CancellationToken cancellationToken = default)
    {
        var page = await _vouchers.GetByCompanyAsync(
            query.CompanyId,
            new PagedRequest(query.Page, query.PageSize),
            query.Status,
            cancellationToken);

        var itemIds = page.Items.SelectMany(v => v.Items.Select(i => i.ItemId)).Distinct().ToList();
        var itemsById = itemIds.Count == 0
            ? new Dictionary<Guid, Item>()
            : (await _items.GetByIdsAsync(itemIds, cancellationToken)).ToDictionary(i => i.Id);

        var receiptIds = page.Items.SelectMany(v => v.Receipts.Select(r => r.PurchaseReceiptId)).Distinct().ToList();
        var receiptNumbers = new Dictionary<Guid, string>();
        foreach (var receiptId in receiptIds)
        {
            var receipt = await _purchases.GetReceiptByIdAsync(receiptId, cancellationToken);
            if (receipt is not null)
            {
                receiptNumbers[receiptId] = receipt.VoucherNo;
            }
        }

        return new PagedResult<LandedCostVoucherDto>(
            page.Items.Select(v => LandedCostVoucherDto.Build(v, itemsById, receiptNumbers)).ToList(),
            page.TotalCount,
            page.PageNumber,
            page.PageSize);
    }
}

public sealed class GetLandedCostVoucherByIdQueryHandler
    : IQueryHandler<GetLandedCostVoucherByIdQuery, LandedCostVoucherDto?>
{
    private readonly ILandedCostVoucherRepository _vouchers;
    private readonly IPurchaseRepository _purchases;
    private readonly IItemRepository _items;

    public GetLandedCostVoucherByIdQueryHandler(
        ILandedCostVoucherRepository vouchers,
        IPurchaseRepository purchases,
        IItemRepository items)
    {
        _vouchers = vouchers;
        _purchases = purchases;
        _items = items;
    }

    public async Task<LandedCostVoucherDto?> HandleAsync(
        GetLandedCostVoucherByIdQuery query, CancellationToken cancellationToken = default)
    {
        var voucher = await _vouchers.GetByIdAsync(query.Id, cancellationToken);
        if (voucher is null || voucher.CompanyId != query.CompanyId)
        {
            return null;
        }

        var itemIds = voucher.Items.Select(i => i.ItemId).Distinct().ToList();
        var itemsById = itemIds.Count == 0
            ? new Dictionary<Guid, Item>()
            : (await _items.GetByIdsAsync(itemIds, cancellationToken)).ToDictionary(i => i.Id);

        var receiptNumbers = new Dictionary<Guid, string>();
        foreach (var receiptId in voucher.Receipts.Select(r => r.PurchaseReceiptId).Distinct())
        {
            var receipt = await _purchases.GetReceiptByIdAsync(receiptId, cancellationToken);
            if (receipt is not null)
            {
                receiptNumbers[receiptId] = receipt.VoucherNo;
            }
        }

        return LandedCostVoucherDto.Build(voucher, itemsById, receiptNumbers);
    }
}
