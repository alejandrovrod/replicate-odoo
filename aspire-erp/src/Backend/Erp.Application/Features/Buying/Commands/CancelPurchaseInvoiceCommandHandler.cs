using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Domain.Services;

namespace Erp.Application.Features.Buying.Commands;

/// <summary>
/// Executes <see cref="CancelPurchaseInvoiceCommand"/>: <c>Unpaid</c>/<c>PartiallyPaid</c>
/// -&gt; <c>Cancelled</c> plus the compensating reversal append, inside ONE transaction
/// (Task 4.6 / spec BY-05 / Constitution III.3).
/// </summary>
/// <remarks>
/// <para><b>Reversal posting date.</b> The reversal rows carry the invoice's ORIGINAL
/// <c>PostingDate</c>, so <c>Company.EnsurePostingDateUnlocked</c> is checked against that date:
/// a frozen original period blocks the CANCELLATION too. The status gate runs after the freeze
/// gate, mirroring <c>CancelJournalEntryCommandHandler</c>.</para>
/// <para><b>No account postability re-check.</b> The accounts were vetted when the bill was
/// posted and the FK constraint on <c>GLEntry.AccountId</c> (Restrict) guarantees they still
/// exist; deactivating an account must never make a bill un-cancellable. The currency snapshot
/// of the original rows is copied verbatim.</para>
/// <para><b>Order of writes.</b> The header is saved BEFORE the reversal rows, exactly like the
/// posting path, so a failure inside the transaction never leaves reversal rows without their
/// cancelled header.</para>
/// </remarks>
public sealed class CancelPurchaseInvoiceCommandHandler
    : ICommandHandler<CancelPurchaseInvoiceCommand, Result<PurchaseInvoiceDto>>
{
    /// <summary>Voucher type stamped on purchase-invoice GL rows (shared with the posting service).</summary>
    private const string InvoiceVoucherType = "PurchaseInvoice";

    private readonly ICompanyRepository _companies;
    private readonly IPurchaseRepository _purchases;
    private readonly IStockRepository _stock;
    private readonly IItemRepository _items;

    public CancelPurchaseInvoiceCommandHandler(
        ICompanyRepository companies,
        IPurchaseRepository purchases,
        IStockRepository stock,
        IItemRepository items)
    {
        _companies = companies;
        _purchases = purchases;
        _stock = stock;
        _items = items;
    }

    public async Task<Result<PurchaseInvoiceDto>> HandleAsync(
        CancelPurchaseInvoiceCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // One transaction for status + reversal rows: the bill ends up Cancelled with a
            // complete mirror image, or untouched - never half-cancelled.
            return await _purchases.ExecuteInTransactionAsync(async token =>
            {
                var invoice = await _purchases.GetInvoiceByIdAsync(command.InvoiceId, token)
                    ?? throw new PurchaseValidationException(
                        PurchaseErrorCodes.InvoiceNotFound,
                        $"Purchase invoice '{command.InvoiceId}' was not found in this tenant.");

                if (invoice.CompanyId != command.CompanyId)
                {
                    throw new PurchaseValidationException(
                        PurchaseErrorCodes.InvoiceNotFound,
                        $"Purchase invoice '{invoice.VoucherNo}' does not belong to company "
                        + $"'{command.CompanyId}'.");
                }

                EnsureRowVersion(invoice, command.RowVersion);

                // Cancelling WRITES ledger rows (the reversal), so the period lock applies -
                // against the invoice's ORIGINAL PostingDate, which is the date the reversal
                // rows will carry.
                var company = await _companies.GetByIdAsync(invoice.CompanyId, token)
                    ?? throw new PurchaseValidationException(
                        PurchaseErrorCodes.CompanyNotFound,
                        $"Company '{invoice.CompanyId}' was not found in this tenant.");

                company.EnsurePostingDateUnlocked(invoice.PostingDate);

                // spec BY-05: status gate FIRST, so a Draft bill reports the transition it
                // cannot make (invalid_status_transition) instead of the missing-ledger code.
                invoice.Cancel();

                var originalRows = (await _stock.GetGlEntriesByVoucherIdAsync(invoice.Id, token))
                    .Where(r => r.VoucherType == InvoiceVoucherType)
                    .ToList();

                if (originalRows.Count == 0)
                {
                    // No ledger rows behind the header: nothing was ever posted for it.
                    throw new PurchaseValidationException(
                        PurchaseErrorCodes.InvoiceNotPosted,
                        $"Purchase invoice '{invoice.VoucherNo}' has no General Ledger rows to reverse.");
                }

                var reversalRows = BuildReversalRows(invoice, originalRows);

                // Constitution III.1 on the rows that are about to be written (a mirror of a
                // balanced voucher is balanced, but the guard proves it rather than assuming it).
                DoubleEntryGuard.EnsureBalanced(reversalRows);

                invoice.OutstandingAmount = 0m;

                await _purchases.UpdateInvoiceAsync(invoice, token);
                await _stock.AddGlEntriesAsync(reversalRows, token);

                var itemIds = invoice.Lines.Select(l => l.ItemId).Distinct().ToList();
                var itemsById = itemIds.Count == 0
                    ? new Dictionary<Guid, Item>()
                    : (await _items.GetByIdsAsync(itemIds, token)).ToDictionary(i => i.Id);

                return Result<PurchaseInvoiceDto>.Success(PurchaseInvoiceDto.Build(invoice, itemsById));
            }, cancellationToken);
        }
        catch (PurchaseValidationException ex)
        {
            return Result<PurchaseInvoiceDto>.Failure(ex.Code, ex.Message);
        }
        catch (DoubleEntryImbalanceException ex)
        {
            return Result<PurchaseInvoiceDto>.Failure(ex.Code, ex.Message);
        }
        catch (FiscalPeriodLockedException ex)
        {
            return Result<PurchaseInvoiceDto>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Result<PurchaseInvoiceDto>.Failure(ex.Code, ex.Message);
        }
    }

    /// <summary>
    /// One mirror row per original GL line: every column copied verbatim except Debit/Credit (and
    /// their account-currency twins), which are SWAPPED, plus the cancellation marker. The
    /// originals are never mutated or deleted (Constitution III.2).
    /// </summary>
    private static List<GLEntry> BuildReversalRows(
        PurchaseInvoice invoice,
        IReadOnlyList<GLEntry> originalRows)
    {
        var reversalRows = new List<GLEntry>(originalRows.Count);

        foreach (var row in originalRows)
        {
            reversalRows.Add(new GLEntry
            {
                // TenantId/CompanyId/PostingDate/Account/Voucher provenance are copied as-is:
                // the reversal belongs to the SAME voucher, dated the ORIGINAL posting date.
                TenantId = row.TenantId,
                CompanyId = row.CompanyId,
                PostingDate = row.PostingDate,
                AccountId = row.AccountId,
                Account = row.Account,

                // The whole point of the compensating entry: sides swapped.
                Debit = row.Credit,
                Credit = row.Debit,
                DebitInAccountCurrency = row.CreditInAccountCurrency,
                CreditInAccountCurrency = row.DebitInAccountCurrency,
                AccountCurrency = row.AccountCurrency,

                VoucherType = row.VoucherType,
                VoucherNo = row.VoucherNo,
                VoucherId = row.VoucherId,
                PartyType = row.PartyType,
                PartyId = row.PartyId,
                CostCenterId = row.CostCenterId,

                IsCancelled = true,
                Remarks = $"Cancellation reversal of {row.VoucherType} {row.VoucherNo} (cancelled)",

                // CreatedAt stays on the column default (SYSDATETIMEOFFSET), so reversal rows
                // sort after the originals they cancel.
            });
        }

        return reversalRows;
    }

    /// <summary>
    /// Compare-and-swap pre-check for the OPTIONAL client token carried by the cancel body
    /// (mirrors <c>JournalPosting.EnsureRowVersion</c>): when the client omits it, the
    /// repository's RowVersion WHERE clause still guards the load/save race.
    /// </summary>
    /// <exception cref="ConcurrencyConflictException">The supplied token is stale.</exception>
    private static void EnsureRowVersion(PurchaseInvoice invoice, byte[]? expected)
    {
        if (expected is null)
        {
            return;
        }

        if (invoice.RowVersion is null || !invoice.RowVersion.AsSpan().SequenceEqual(expected))
        {
            throw new ConcurrencyConflictException(nameof(PurchaseInvoice), invoice.Id);
        }
    }
}
