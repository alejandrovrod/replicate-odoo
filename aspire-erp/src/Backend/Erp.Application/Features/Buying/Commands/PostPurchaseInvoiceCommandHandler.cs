using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Services;
using Erp.Domain.Exceptions;

namespace Erp.Application.Features.Buying.Commands;

/// <summary>
/// Executes <see cref="PostPurchaseInvoiceCommand"/>: maps the command to the posting request and
/// translates domain failures into <see cref="Result{T}"/> failures (4xx), mirroring
/// PostPurchaseReceiptCommandHandler. Configuration faults deliberately bubble up as 500.
/// </summary>
public sealed class PostPurchaseInvoiceCommandHandler
    : ICommandHandler<PostPurchaseInvoiceCommand, Result<PurchaseInvoicePostingDto>>
{
    private readonly IPurchasePostingService _posting;

    public PostPurchaseInvoiceCommandHandler(IPurchasePostingService posting)
    {
        _posting = posting;
    }

    public async Task<Result<PurchaseInvoicePostingDto>> HandleAsync(
        PostPurchaseInvoiceCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new PurchaseInvoicePostingRequest(
                command.CompanyId,
                command.PurchaseReceiptId,
                command.PostingDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                command.TaxAmount,
                (command.Lines ?? Array.Empty<PostPurchaseInvoiceLine>())
                    .Select(l => new PurchaseInvoicePostingLine(
                        l.PurchaseReceiptLineId, l.ItemId, l.Qty, l.Rate))
                    .ToList());

            var result = await _posting.PostInvoiceAsync(request, cancellationToken);
            return Result<PurchaseInvoicePostingDto>.Success(result);
        }
        catch (PurchaseValidationException ex)
        {
            return Result<PurchaseInvoicePostingDto>.Failure(ex.Code, ex.Message);
        }
        catch (StockValidationException ex)
        {
            return Result<PurchaseInvoicePostingDto>.Failure(ex.Code, ex.Message);
        }
    }
}
