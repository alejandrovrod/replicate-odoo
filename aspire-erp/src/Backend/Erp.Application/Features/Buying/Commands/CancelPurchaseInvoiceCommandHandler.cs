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
/// <para><b>Stock unwind (spec BY-05 third clause).</b> Cancelling the bill also reverses the
/// physical stock intake of every purchase receipt behind it, in the SAME transaction: one
/// negated <c>StockLedgerEntry</c> per intake row (QtyChange/Amount negated, original FIFO
/// rates preserved) plus the swapped mirror of the receipt's GL rows, both flagged
/// <c>IsCancelled = true</c> on the REVERSAL rows only - every original row stays byte-identical
/// (Constitution III.2). The reversal rows carry the RECEIPT's original posting date, so each
/// distinct receipt gets its own <c>Company.EnsurePostingDateUnlocked</c> freeze gate: a frozen
/// receipt period blocks the whole cancellation (all-or-nothing, checked before any write).
/// </para>
/// <para><b>DOCUMENTED BOUNDARY - unwind granularity is per-RECEIPT.</b> The skip-if-unwound gate
/// looks at the receipt voucher as a whole: when ANY intake row of that voucher is already
/// flagged <c>IsCancelled</c> the receipt is skipped entirely (idempotency) while the invoice
/// itself still cancels and mirrors its own GL. Consequence: two invoices billing different
/// lines of one SHARED receipt unwind the WHOLE receipt on the first cancel. The billing
/// invariant (each invoice line bills its receipt line in full, and an invoice covers its
/// receipts) makes the 1:1 case exact; the shared-receipt case is this recorded limitation,
/// not silent behaviour.</para>
/// </remarks>
public sealed class CancelPurchaseInvoiceCommandHandler
    : ICommandHandler<CancelPurchaseInvoiceCommand, Result<PurchaseInvoiceDto>>
{
    /// <summary>Voucher type stamped on purchase-invoice GL rows (shared with the posting service).</summary>
    private const string InvoiceVoucherType = "PurchaseInvoice";

    /// <summary>Voucher type stamped on purchase-receipt intake SLE/GL rows (shared with the posting service).</summary>
    private const string ReceiptVoucherType = "PurchaseReceipt";

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
                // R-13 FC-04: cancelling into a closed year is refused — the close is immutable.
                await _companies.EnsurePostingDateInOpenYearAsync(company.Id, invoice.PostingDate, token);

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

                var reversalRows = BuildReversalRows(originalRows);

                // spec BY-05 third clause: the bill's cancellation also unwinds the physical
                // stock intake of every receipt behind it (planned here, written below - still
                // zero writes so far, so any gate below leaves the ledgers untouched).
                var unwind = await PlanReceiptUnwindAsync(invoice, company, token);

                // One list, invoice mirror FIRST: the write below appends the invoice reversal,
                // then the receipt mirrors, in voucher order.
                var allGlReversals = new List<GLEntry>(reversalRows.Count + unwind.GlReversals.Count);
                allGlReversals.AddRange(reversalRows);
                allGlReversals.AddRange(unwind.GlReversals);

                // Constitution III.1 on ALL rows that are about to be written (a mirror of a
                // balanced voucher is balanced, but the guard proves it rather than assuming it).
                DoubleEntryGuard.EnsureBalanced(allGlReversals);

                invoice.OutstandingAmount = 0m;

                await _purchases.UpdateInvoiceAsync(invoice, token);
                await _stock.AddGlEntriesAsync(allGlReversals, token);

                if (unwind.SleReversals.Count > 0)
                {
                    await _stock.AddLedgerEntriesAsync(unwind.SleReversals, token);
                }

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
    /// originals are never mutated or deleted (Constitution III.2). Shared by the invoice mirror
    /// and the per-receipt mirrors: the remarks name the voucher each row reverses.
    /// </summary>
    private static List<GLEntry> BuildReversalRows(IReadOnlyList<GLEntry> originalRows)
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
    /// Plans the physical stock unwind for the receipts behind the invoice: resolves the distinct
    /// receipt aggregates, freeze-gates each one against ITS posting date, then builds the negated
    /// Kardex rows plus the swapped receipt-GL mirrors for every receipt that was not already
    /// unwound. Pure planning - performs reads only, so every failure below is a zero-write
    /// rejection and the invoice itself is left to the transaction rollback.
    /// </summary>
    /// <exception cref="PurchaseValidationException">
    /// <see cref="PurchaseErrorCodes.PurchaseReceiptNotFound"/> when a billed receipt line (or its
    /// receipt) does not exist in this tenant, or belongs to another company (reported as not
    /// found: the id must not leak across companies).
    /// </exception>
    /// <exception cref="FiscalPeriodLockedException">
    /// A receipt behind the bill is dated inside the frozen period.
    /// </exception>
    private async Task<ReceiptUnwindPlan> PlanReceiptUnwindAsync(
        PurchaseInvoice invoice,
        Company company,
        CancellationToken token)
    {
        var plan = new ReceiptUnwindPlan();

        var receiptLineIds = invoice.Lines.Select(l => l.PurchaseReceiptLineId).Distinct().ToList();
        if (receiptLineIds.Count == 0)
        {
            // A bill with no receipt linkage (legacy shape): nothing physical to unwind, the
            // invoice mirror above is the whole cancellation.
            return plan;
        }

        var receiptLines = await _purchases.GetReceiptLinesByIdsAsync(receiptLineIds, token);
        var linesById = receiptLines.ToDictionary(l => l.Id);

        foreach (var id in receiptLineIds)
        {
            if (!linesById.ContainsKey(id))
            {
                throw new PurchaseValidationException(
                    PurchaseErrorCodes.PurchaseReceiptNotFound,
                    $"Purchase invoice '{invoice.VoucherNo}' bills receipt line '{id}', "
                    + "which was not found in this tenant.");
            }
        }

        var receipts = new List<PurchaseReceipt>();
        foreach (var receiptId in receiptLines.Select(l => l.PurchaseReceiptId).Distinct())
        {
            var receipt = await _purchases.GetReceiptByIdAsync(receiptId, token)
                ?? throw new PurchaseValidationException(
                    PurchaseErrorCodes.PurchaseReceiptNotFound,
                    $"Purchase receipt '{receiptId}' billed by invoice '{invoice.VoucherNo}' "
                    + "was not found in this tenant.");

            if (receipt.CompanyId != invoice.CompanyId)
            {
                throw new PurchaseValidationException(
                    PurchaseErrorCodes.PurchaseReceiptNotFound,
                    $"Purchase receipt '{receipt.VoucherNo}' does not belong to company "
                    + $"'{invoice.CompanyId}'.");
            }

            receipts.Add(receipt);
        }

        // Freeze gate per receipt, BEFORE any mutation: the reversal rows carry the RECEIPT's
        // original date, so a frozen receipt period blocks the whole cancellation.
        foreach (var receipt in receipts)
        {
            company.EnsurePostingDateUnlocked(receipt.PostingDate);
            // R-13 FC-04: a frozen-by-close receipt period blocks the whole cancellation.
            await _companies.EnsurePostingDateInOpenYearAsync(company.Id, receipt.PostingDate, token);
        }

        foreach (var receipt in receipts)
        {
            var intakeRows = (await _stock.GetLedgerEntriesByVoucherAsync(receipt.VoucherNo, token))
                .Where(s => s.VoucherType == ReceiptVoucherType)
                .ToList();

            // Skip-if-unwound: ANY cancelled row on the receipt voucher means the intake was
            // already reversed (idempotency + the shared-receipt safety valve - see the class
            // remarks). The invoice itself still cancels and mirrors its own GL below.
            if (intakeRows.Any(s => s.IsCancelled))
            {
                continue;
            }

            var createdAt = DateTimeOffset.UtcNow;
            foreach (var intake in intakeRows.Where(s => !s.IsCancelled))
            {
                plan.SleReversals.Add(new StockLedgerEntry
                {
                    Id = Guid.NewGuid(),
                    TenantId = intake.TenantId,
                    ItemId = intake.ItemId,
                    WarehouseId = intake.WarehouseId,
                    StockEntryId = intake.StockEntryId,
                    VoucherType = intake.VoucherType,
                    VoucherNo = intake.VoucherNo,
                    PostingDate = intake.PostingDate,

                    // Verbatim negation: the FIFO valuation rate is PRESERVED exactly, only the
                    // signed movements flip - the same shape as CancelStockEntryCommandHandler.
                    QtyChange = -intake.QtyChange,
                    ValuationRate = intake.ValuationRate,
                    Amount = -intake.Amount,
                    CreatedAt = createdAt,
                    IsCancelled = true,
                });
            }

            var receiptGlRows = (await _stock.GetGlEntriesByVoucherIdAsync(receipt.Id, token))
                .Where(r => r.VoucherType == ReceiptVoucherType)
                .ToList();

            plan.GlReversals.AddRange(BuildReversalRows(receiptGlRows));
        }

        return plan;
    }

    /// <summary>Buffered unwind output: receipt GL mirrors first-class with the invoice mirror, then the Kardex negations.</summary>
    private sealed class ReceiptUnwindPlan
    {
        public List<GLEntry> GlReversals { get; } = new();

        public List<StockLedgerEntry> SleReversals { get; } = new();
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
