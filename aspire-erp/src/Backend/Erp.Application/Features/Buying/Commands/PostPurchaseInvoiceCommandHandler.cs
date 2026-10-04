using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Services;
using Erp.Domain.Entities;
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
                command.SupplierId,
                command.BillNumber,
                command.PostingDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                command.DueDate ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30),
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
        catch (ConcurrencyConflictException ex)
        {
            // Spec BY-06: the order status changed between the load and the save (RowVersion).
            return Result<PurchaseInvoicePostingDto>.Failure(ex.Code, ex.Message);
        }
        catch (FiscalPeriodLockedException ex)
        {
            // tasks.md 2.2 / spec AC-04: PostingDate <= Company.FrozenAccountsDate. The service
            // threw BEFORE building any GLEntry line, so the failure carries zero data changes.
            return Result<PurchaseInvoicePostingDto>.Failure(ex.Code, ex.Message);
        }
        catch (OverbillingNotAllowedException ex)
        {
            // Task 4.4 / spec BY-03 and BY-06: the cumulative three-way match rejected the bill
            // (attemptingToBillQty > receivedQty - previouslyBilledQty). The service threw before
            // voucher numbering and before any GLEntry line, so no ledger row exists.
            return Result<PurchaseInvoicePostingDto>.Failure(
                PurchaseErrorCodes.OverbillingNotAllowed, ex.Message);
        }
    }
}
