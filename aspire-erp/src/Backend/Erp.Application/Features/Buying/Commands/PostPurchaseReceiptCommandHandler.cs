using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Services;
using Erp.Domain.Exceptions;

namespace Erp.Application.Features.Buying.Commands;

/// <summary>
/// Executes <see cref="PostPurchaseReceiptCommand"/>: maps the command to the posting request and
/// translates domain failures into <see cref="Result{T}"/> failures (4xx), mirroring
/// CreateStockEntryCommandHandler. Configuration faults deliberately bubble up as 500.
/// </summary>
public sealed class PostPurchaseReceiptCommandHandler
    : ICommandHandler<PostPurchaseReceiptCommand, Result<PurchaseReceiptPostingDto>>
{
    private readonly IPurchasePostingService _posting;

    public PostPurchaseReceiptCommandHandler(IPurchasePostingService posting)
    {
        _posting = posting;
    }

    public async Task<Result<PurchaseReceiptPostingDto>> HandleAsync(
        PostPurchaseReceiptCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new PurchaseReceiptPostingRequest(
                command.CompanyId,
                command.WarehouseId,
                command.PurchaseOrderId,
                command.PostingDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                (command.Lines ?? Array.Empty<PostPurchaseReceiptLine>())
                    .Select(l => new PurchaseReceiptPostingLine(l.ItemId, l.Qty, l.Rate))
                    .ToList());

            var result = await _posting.PostReceiptAsync(request, cancellationToken);
            return Result<PurchaseReceiptPostingDto>.Success(result);
        }
        catch (PurchaseValidationException ex)
        {
            return Result<PurchaseReceiptPostingDto>.Failure(ex.Code, ex.Message);
        }
        catch (StockValidationException ex)
        {
            return Result<PurchaseReceiptPostingDto>.Failure(ex.Code, ex.Message);
        }
    }
}
