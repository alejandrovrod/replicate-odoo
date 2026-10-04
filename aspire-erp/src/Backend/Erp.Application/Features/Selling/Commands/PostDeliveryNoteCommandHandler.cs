using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Services;
using Erp.Domain.Exceptions;

namespace Erp.Application.Features.Selling.Commands;

/// <summary>
/// Executes <see cref="PostDeliveryNoteCommand"/>: maps the command to the posting request and
/// translates domain failures into <see cref="Result{T}"/> failures (4xx), mirroring
/// PostPurchaseReceiptCommandHandler. Configuration faults deliberately bubble up as 500.
/// </summary>
/// <remarks>
/// Status mapping (the established pipeline): <c>sales_order_not_deliverable</c>,
/// <c>fiscal_period_locked</c> and <c>concurrency_conflict</c> are STATE conflicts -&gt; 409;
/// <c>overdelivery_not_allowed</c>, <c>insufficient_stock</c> and the field/validation codes
/// describe a bad REQUEST -&gt; 400; <c>sales_order_not_found</c> -&gt; 404 (the controller).
/// </remarks>
public sealed class PostDeliveryNoteCommandHandler
    : ICommandHandler<PostDeliveryNoteCommand, Result<DeliveryNotePostingDto>>
{
    private readonly ISalesPostingService _posting;

    public PostDeliveryNoteCommandHandler(ISalesPostingService posting)
    {
        _posting = posting;
    }

    public async Task<Result<DeliveryNotePostingDto>> HandleAsync(
        PostDeliveryNoteCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new DeliveryNotePostingRequest(
                command.CompanyId,
                command.SalesOrderId,
                command.WarehouseId,
                command.PostingDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                (command.Lines ?? Array.Empty<PostDeliveryNoteLine>())
                    .Select(l => new DeliveryNotePostingLine(l.SalesOrderItemId, l.ItemId, l.Qty))
                    .ToList());

            var result = await _posting.PostDeliveryNoteAsync(request, cancellationToken);
            return Result<DeliveryNotePostingDto>.Success(result);
        }
        catch (SalesValidationException ex)
        {
            return Result<DeliveryNotePostingDto>.Failure(ex.Code, ex.Message);
        }
        catch (StockValidationException ex)
        {
            return Result<DeliveryNotePostingDto>.Failure(ex.Code, ex.Message);
        }
        catch (OverdeliveryNotAllowedException ex)
        {
            // spec SL-04: the note ships more than the order line still owes (zero rows written).
            return Result<DeliveryNotePostingDto>.Failure(ex.Code, ex.Message);
        }
        catch (InsufficientStockException ex)
        {
            return Result<DeliveryNotePostingDto>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            // The order status/counters changed between the load and the save (RowVersion).
            return Result<DeliveryNotePostingDto>.Failure(ex.Code, ex.Message);
        }
        catch (FiscalPeriodLockedException ex)
        {
            // tasks.md 2.2 / spec AC-04: PostingDate <= Company.FrozenAccountsDate. The service
            // threw BEFORE building any GLEntry line, so the failure carries zero data changes.
            return Result<DeliveryNotePostingDto>.Failure(ex.Code, ex.Message);
        }
        catch (NotSupportedException ex)
        {
            // Same contract as CreateStockEntryCommandHandler: a non-FIFO item cannot be valued.
            return Result<DeliveryNotePostingDto>.Failure("unsupported_valuation_method", ex.Message);
        }
    }
}
