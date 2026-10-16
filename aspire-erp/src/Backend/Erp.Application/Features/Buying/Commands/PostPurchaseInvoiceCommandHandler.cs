using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Services;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

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
    private readonly ISupplierRepository _suppliers;

    public PostPurchaseInvoiceCommandHandler(IPurchasePostingService posting, ISupplierRepository suppliers)
    {
        _posting = posting;
        _suppliers = suppliers;
    }

    public async Task<Result<PurchaseInvoicePostingDto>> HandleAsync(
        PostPurchaseInvoiceCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Governance guard (module buying-suppliers, spec §1 gap D4, scenario SUP-G2): a
            // supplier on hold for INVOICES (or ALL) rejects the bill BEFORE the posting service
            // builds any row, so the failure carries zero ledger writes. The release date is
            // honored by Supplier.IsBlockedFor (scenario SUP-G3: an elapsed release date allows it).
            var postingDate = command.PostingDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var supplier = await _suppliers.GetByIdAsync(command.SupplierId, cancellationToken)
                ?? throw new PurchaseValidationException(
                    PurchaseErrorCodes.SupplierNotFound,
                    $"Supplier '{command.SupplierId}' was not found in this tenant.");

            if (supplier.IsBlockedFor(SupplierHoldType.Invoices, postingDate))
            {
                throw new PurchaseValidationException(
                    PurchaseErrorCodes.SupplierOnHoldInvoices,
                    $"Supplier '{supplier.Code}' is on hold for invoices and cannot be billed"
                    + (supplier.ReleaseDate.HasValue ? $" until {supplier.ReleaseDate:yyyy-MM-dd}." : "."));
            }

            var request = new PurchaseInvoicePostingRequest(
                command.CompanyId,
                command.SupplierId,
                command.BillNumber,
                command.CurrencyId,
                postingDate,
                command.DueDate ?? postingDate.AddDays(30),
                command.TaxAmount,
                (command.Lines ?? Array.Empty<PostPurchaseInvoiceLine>())
                    .Select(l => new PurchaseInvoicePostingLine(
                        l.PurchaseReceiptLineId, l.ItemId, l.Qty, l.Rate))
                    .ToList(),
                command.Taxes,
                command.TaxTemplateId);

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
        catch (AssetValidationException ex)
        {
            // Asset-capitalization module (gap D5): a fixed-asset line with a misconfigured category,
            // a missing cost account or a billed-rate mismatch fails the whole bill with its own
            // RFC 7807 code (e.g. asset_category_missing_accounts) before anything is persisted.
            return Result<PurchaseInvoicePostingDto>.Failure(ex.Code, ex.Message);
        }
        catch (LifecycleHookRejectedException ex)
        {
            // Extensibility Fase 1: the hook's own stable code travels verbatim; the posting
            // transaction rolled back, so no invoice and no GL exist.
            return Result<PurchaseInvoicePostingDto>.Failure(ex.Code, ex.Message);
        }
        catch (TaxValidationException ex)
        {
            // Tax-engine Fase 2: engine/template failures carry their own RFC 7807 codes
            // (tax_account_required, invalid_tax_calculation_basis, tax_template_not_found...).
            return Result<PurchaseInvoicePostingDto>.Failure(ex.Code, ex.Message);
        }
        catch (WorkflowValidationException ex)
        {
            // Workflow Fase 5: the guard's own stable code travels verbatim
            // (workflow_submit_not_allowed); the posting rolled back with zero writes.
            return Result<PurchaseInvoicePostingDto>.Failure(ex.Code, ex.Message);
        }
    }
}
