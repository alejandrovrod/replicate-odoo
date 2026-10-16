using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Services;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;

namespace Erp.Application.Features.Buying.Commands;

/// <summary>
/// Executes <see cref="PostPurchaseReturnCommand"/>: maps the command to the posting request and
/// translates domain failures into <see cref="Result{T}"/> failures (4xx), mirroring
/// PostPurchaseInvoiceCommandHandler. Configuration faults deliberately bubble up as 500.
/// </summary>
public sealed class PostPurchaseReturnCommandHandler
    : ICommandHandler<PostPurchaseReturnCommand, Result<PurchaseInvoicePostingDto>>
{
    private readonly IPurchasePostingService _posting;

    public PostPurchaseReturnCommandHandler(IPurchasePostingService posting)
    {
        _posting = posting;
    }

    public async Task<Result<PurchaseInvoicePostingDto>> HandleAsync(
        PostPurchaseReturnCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new PurchaseReturnPostingRequest(
                command.CompanyId,
                command.ReturnAgainstId,
                command.BillNumber,
                command.CurrencyId,
                command.PostingDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                command.DueDate ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30),
                command.TaxAmount,
                command.UpdateStock,
                (command.Lines ?? Array.Empty<PostPurchaseReturnLine>())
                    .Select(l => new PurchaseReturnPostingLine(l.PurchaseInvoiceLineId, l.Qty))
                    .ToList());

            var result = await _posting.PostReturnAsync(request, cancellationToken);
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
            // Spec BY-06: the original bill changed between the load and the save (RowVersion).
            return Result<PurchaseInvoicePostingDto>.Failure(ex.Code, ex.Message);
        }
        catch (FiscalPeriodLockedException ex)
        {
            // tasks.md 2.2 / spec AC-04: PostingDate <= Company.FrozenAccountsDate. The service
            // threw BEFORE building any GLEntry line, so the failure carries zero data changes.
            return Result<PurchaseInvoicePostingDto>.Failure(ex.Code, ex.Message);
        }
        catch (LifecycleHookRejectedException ex)
        {
            // Extensibility Fase 1: the hook's own stable code travels verbatim; the posting
            // transaction rolled back, so no debit note and no GL exist.
            return Result<PurchaseInvoicePostingDto>.Failure(ex.Code, ex.Message);
        }
        catch (WorkflowValidationException ex)
        {
            // Workflow Fase 5: the guard's own stable code travels verbatim.
            return Result<PurchaseInvoicePostingDto>.Failure(ex.Code, ex.Message);
        }
    }
}
