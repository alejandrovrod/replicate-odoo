using Erp.Application.Features.Buying.Commands;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Task 4.6 / spec BY-05: the purchase-invoice cancellation pipeline exercised through the CQRS
/// handler against in-memory repository doubles (Constitution I.2/I.3) - the append-only
/// compensating reversal, the status transition, the "rejected cancellation writes ZERO ledger
/// rows" guarantee and the optional compare-and-swap RowVersion.
/// </summary>
public sealed class CancelPurchaseInvoiceCommandHandlerTests
{
    private const string InvoiceVoucherType = "PurchaseInvoice";

    private const string ReceiptVoucherType = "PurchaseReceipt";

    private static readonly DateOnly PostingDate = new(2026, 3, 2);

    /// <summary>A posting date INSIDE the frozen period used by the freeze test.</summary>
    private static readonly DateOnly BackDated = new(2025, 6, 15);

    private static readonly DateOnly FrozenThrough = new(2025, 12, 31);

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly FakeCompanyRepository _companies = new();
    private readonly FakePurchaseRepository _purchases = new();
    private readonly FakeStockRepository _stock = new();
    private readonly FakeItemRepository _items = new();

    public CancelPurchaseInvoiceCommandHandlerTests()
    {
        _companies.Company = new Company
        {
            Id = _companyId,
            TenantId = Guid.NewGuid(),
            Name = "Acme Industrial",
        };
    }

    private CancelPurchaseInvoiceCommandHandler Handler() =>
        new(_companies, _purchases, _stock, _items);

    /// <summary>
    /// Seeds a posted bill WITH its receipt linkage - cancellation tests start from persisted
    /// aggregates, never from the posting handler. The invoice line bills the receipt line in
    /// full (the repo's billing invariant), the receipt carries one intake Kardex row (persisted
    /// only, so <c>AddedLedger</c> holds just the reversals the test triggers) and - when
    /// <paramref name="withGlRows"/> - the five seeded GL rows (3 invoice + 2 receipt accrual).
    /// </summary>
    private async Task<SeededBill> SeedInvoice(
        PurchaseInvoiceStatus status,
        DateOnly postingDate,
        Guid? companyId = null,
        byte[]? rowVersion = null,
        bool withGlRows = true,
        DateOnly? receiptPostingDate = null,
        Guid? receiptCompanyId = null)
    {
        var company = companyId ?? _companyId;
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        var receipt = new PurchaseReceipt
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = receiptCompanyId ?? company,
            SupplierId = Guid.NewGuid(),
            WarehouseId = warehouseId,
            PostingDate = receiptPostingDate ?? postingDate,
            VoucherNo = "PR-2026-00007",
            Status = PurchaseReceiptStatus.Submitted,
            TotalAmount = 1000m,
            CreatedAt = DateTimeOffset.UtcNow,
            Lines = new List<PurchaseReceiptLine>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ItemId = itemId,
                    Qty = 10m,
                    Rate = 100m,
                    Amount = 1000m,
                    LineNumber = 1,
                },
            },
        };

        foreach (var receiptLine in receipt.Lines)
        {
            receiptLine.PurchaseReceiptId = receipt.Id;
        }

        _purchases.SeedReceipt(receipt);
        var billedLine = receipt.Lines.Single();

        var invoice = new PurchaseInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = company,
            SupplierId = Guid.NewGuid(),
            BillNumber = "BILL-77",
            PostingDate = postingDate,
            DueDate = postingDate.AddDays(30),
            Status = status,
            NetTotal = 1000m,
            TaxTotal = 100m,
            WithholdingTaxTotal = 0m,
            GrandTotal = 1100m,
            OutstandingAmount = status is PurchaseInvoiceStatus.Cancelled ? 0m : 1100m,
            VoucherNo = "PINV-2026-00042",
            RowVersion = rowVersion ?? new byte[] { 0x01, 0x02, 0x03 },
            CreatedAt = DateTimeOffset.UtcNow,
            Lines = new List<PurchaseInvoiceLine>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ItemId = itemId,
                    PurchaseReceiptLineId = billedLine.Id,
                    Qty = 10m,
                    Rate = 100m,
                    Amount = 1000m,
                    LineNumber = 1,
                },
            },
        };

        _purchases.SeedInvoice(invoice);

        // The physical intake: +10 @ $100 into the receipt warehouse, unflagged.
        var intake = new StockLedgerEntry
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ItemId = itemId,
            WarehouseId = warehouseId,
            StockEntryId = null,
            VoucherType = ReceiptVoucherType,
            VoucherNo = receipt.VoucherNo,
            PostingDate = receipt.PostingDate,
            QtyChange = 10m,
            ValuationRate = 100m,
            Amount = 1000m,
            CreatedAt = DateTimeOffset.UtcNow,
            IsCancelled = false,
        };
        _stock.SeedLedger(intake);

        if (withGlRows)
        {
            // BY-01's canonical triple: Dr 2120 1000 / Dr 1130 100 / Cr 2110 1100.
            // Plus the receipt accrual pair: Dr 1310 1000 / Cr 2120 1000.
            await _stock.AddGlEntriesAsync(new[]
            {
                NewGlRow(invoice, accountCode: "2120", debit: 1000m, credit: 0m),
                NewGlRow(invoice, accountCode: "1130", debit: 100m, credit: 0m),
                NewGlRow(invoice, accountCode: "2110", debit: 0m, credit: 1100m),
                NewReceiptGlRow(receipt, accountCode: "1310", debit: 1000m, credit: 0m),
                NewReceiptGlRow(receipt, accountCode: "2120", debit: 0m, credit: 1000m),
            });
        }

        return new SeededBill(invoice, receipt, billedLine.Id, itemId, warehouseId, intake);
    }

    /// <summary>A seeded bill plus the linkage its cancellation unwinds.</summary>
    private sealed record SeededBill(
        PurchaseInvoice Invoice,
        PurchaseReceipt Receipt,
        Guid ReceiptLineId,
        Guid ItemId,
        Guid WarehouseId,
        StockLedgerEntry Intake);

    private static GLEntry NewGlRow(PurchaseInvoice invoice, string accountCode, decimal debit, decimal credit) =>
        new()
        {
            CompanyId = invoice.CompanyId,
            PostingDate = invoice.PostingDate,
            AccountId = Guid.NewGuid(),
            Debit = debit,
            Credit = credit,
            DebitInAccountCurrency = debit,
            CreditInAccountCurrency = credit,
            AccountCurrency = "USD",
            VoucherType = InvoiceVoucherType,
            VoucherNo = invoice.VoucherNo,
            VoucherId = invoice.Id,
            IsCancelled = false,
            Remarks = $"PurchaseInvoice: {accountCode}",
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private static GLEntry NewReceiptGlRow(PurchaseReceipt receipt, string accountCode, decimal debit, decimal credit) =>
        new()
        {
            CompanyId = receipt.CompanyId,
            PostingDate = receipt.PostingDate,
            AccountId = Guid.NewGuid(),
            Debit = debit,
            Credit = credit,
            DebitInAccountCurrency = debit,
            CreditInAccountCurrency = credit,
            AccountCurrency = "USD",
            VoucherType = ReceiptVoucherType,
            VoucherNo = receipt.VoucherNo,
            VoucherId = receipt.Id,
            IsCancelled = false,
            Remarks = $"PurchaseReceipt: {accountCode}",
            CreatedAt = DateTimeOffset.UtcNow,
        };

    // ------------------------------------------------------------------ happy path (spec BY-05)

    [Fact]
    public async Task Cancel_UnpaidInvoice_AppendsSwappedReversalAndKeepsOriginals()
    {
        var bill = await SeedInvoice(PurchaseInvoiceStatus.Unpaid, PostingDate);
        var invoice = bill.Invoice;

        // Snapshots of the ORIGINALS taken before the cancellation: the append-only guarantee
        // (Constitution III.2) means these values must be untouched afterwards - the invoice GL
        // triple, the receipt accrual pair AND the physical intake row.
        var glBefore = _stock.AddedGlEntries
            .Select(r => (r.VoucherId, r.AccountId, r.Debit, r.Credit, r.IsCancelled, r.Remarks))
            .ToList();
        var intakeBefore = SnapshotSle(bill.Intake);

        var result = await Handler().HandleAsync(
            new CancelPurchaseInvoiceCommand(_companyId, invoice.Id));

        // spec BY-05: status transition + zeroed outstanding balance.
        Assert.True(result.IsSuccess);
        Assert.Equal(PurchaseInvoiceStatus.Cancelled, result.Value!.Status);
        Assert.Equal(0m, result.Value.OutstandingAmount);
        Assert.Equal(PurchaseInvoiceStatus.Cancelled, invoice.Status);
        Assert.Equal(0m, invoice.OutstandingAmount);
        Assert.Equal(1, _purchases.TransactionCount);

        // Five originals (3 invoice + 2 receipt) + five reversal rows.
        Assert.Equal(10, _stock.AddedGlEntries.Count);
        var reversals = _stock.AddedGlEntries.Where(r => r.IsCancelled).ToList();
        Assert.Equal(5, reversals.Count);

        // The invoice mirror: sides SWAPPED, same voucher identity.
        var invoiceOriginals = _stock.AddedGlEntries
            .Where(r => !r.IsCancelled && r.VoucherId == invoice.Id)
            .ToList();
        Assert.Equal(3, invoiceOriginals.Count);
        var invoiceReversals = reversals.Where(r => r.VoucherId == invoice.Id).ToList();
        Assert.Equal(3, invoiceReversals.Count);

        foreach (var original in invoiceOriginals)
        {
            var reversal = Assert.Single(invoiceReversals, r => r.AccountId == original.AccountId);

            Assert.Equal(original.Credit, reversal.Debit);
            Assert.Equal(original.Debit, reversal.Credit);
            Assert.Equal(original.VoucherId, reversal.VoucherId);
            Assert.Equal(original.VoucherNo, reversal.VoucherNo);
            Assert.Equal(original.VoucherType, reversal.VoucherType);
            Assert.Equal(original.PostingDate, reversal.PostingDate);
            Assert.Equal(original.PartyId, reversal.PartyId);
            Assert.True(reversal.IsCancelled);
            Assert.Contains(invoice.VoucherNo, reversal.Remarks);
        }

        // The receipt GL mirror: the accrual pair nets out under the RECEIPT voucher identity
        // and the RECEIPT's original posting date.
        var receiptReversals = reversals.Where(r => r.VoucherId == bill.Receipt.Id).ToList();
        Assert.Equal(2, receiptReversals.Count);

        foreach (var reversal in receiptReversals)
        {
            Assert.Equal(ReceiptVoucherType, reversal.VoucherType);
            Assert.Equal(bill.Receipt.VoucherNo, reversal.VoucherNo);
            Assert.Equal(bill.Receipt.PostingDate, reversal.PostingDate);
            Assert.True(reversal.IsCancelled);
            Assert.Contains(bill.Receipt.VoucherNo, reversal.Remarks);
        }

        // ...and the originals themselves were NEVER mutated.
        Assert.Equal(
            glBefore,
            _stock.AddedGlEntries
                .Where(r => !r.IsCancelled)
                .Select(r => (r.VoucherId, r.AccountId, r.Debit, r.Credit, r.IsCancelled, r.Remarks)));
        Assert.Equal(intakeBefore, SnapshotSle(bill.Intake));

        // Trial-balance neutrality: debits == credits across originals + reversals, and every
        // account nets to zero individually.
        Assert.Equal(
            _stock.AddedGlEntries.Sum(r => r.Debit),
            _stock.AddedGlEntries.Sum(r => r.Credit));
        Assert.All(
            _stock.AddedGlEntries.GroupBy(r => r.AccountId),
            g => Assert.Equal(g.Sum(r => r.Debit), g.Sum(r => r.Credit)));

        // The physical unwind: ONE negated Kardex row - same provenance, FIFO rate preserved,
        // signed movements flipped, flagged as the reversal.
        var sleReversal = Assert.Single(_stock.AddedLedger);
        Assert.NotEqual(bill.Intake.Id, sleReversal.Id);
        Assert.Equal(bill.ItemId, sleReversal.ItemId);
        Assert.Equal(bill.WarehouseId, sleReversal.WarehouseId);
        Assert.Equal(bill.Intake.StockEntryId, sleReversal.StockEntryId);
        Assert.Equal(ReceiptVoucherType, sleReversal.VoucherType);
        Assert.Equal(bill.Receipt.VoucherNo, sleReversal.VoucherNo);
        Assert.Equal(bill.Receipt.PostingDate, sleReversal.PostingDate);
        Assert.Equal(-bill.Intake.QtyChange, sleReversal.QtyChange);
        Assert.Equal(bill.Intake.ValuationRate, sleReversal.ValuationRate);
        Assert.Equal(-bill.Intake.Amount, sleReversal.Amount);
        Assert.True(sleReversal.IsCancelled);

        // Kardex net zero for the receipt voucher: intake + reversal cancel out.
        var kardex = await _stock.GetLedgerEntriesByVoucherAsync(bill.Receipt.VoucherNo);
        Assert.Equal(0m, kardex.Sum(r => r.QtyChange));
        Assert.Equal(0m, kardex.Sum(r => r.Amount));
    }

    /// <summary>Full-field snapshot of a Kardex row for the byte-identical assertion.</summary>
    private static (Guid, Guid, Guid, Guid, Guid?, string, string, DateOnly, decimal, decimal, decimal, bool) SnapshotSle(
        StockLedgerEntry row) =>
        (row.Id, row.TenantId, row.ItemId, row.WarehouseId, row.StockEntryId,
            row.VoucherType, row.VoucherNo, row.PostingDate,
            row.QtyChange, row.ValuationRate, row.Amount, row.IsCancelled);

    [Fact]
    public async Task Cancel_PartiallyPaidInvoice_CancelsAndZeroesOutstanding()
    {
        var bill = await SeedInvoice(PurchaseInvoiceStatus.PartiallyPaid, PostingDate);
        var invoice = bill.Invoice;

        var result = await Handler().HandleAsync(
            new CancelPurchaseInvoiceCommand(_companyId, invoice.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(PurchaseInvoiceStatus.Cancelled, result.Value!.Status);
        Assert.Equal(0m, result.Value.OutstandingAmount);
    }

    // ----------------------------------------------------------------------- rejection paths

    [Fact]
    public async Task Cancel_DraftInvoice_FailsWithInvalidStatusTransitionAndZeroLedgerRows()
    {
        var bill = await SeedInvoice(PurchaseInvoiceStatus.Draft, PostingDate, withGlRows: false);
        var invoice = bill.Invoice;

        var result = await Handler().HandleAsync(
            new CancelPurchaseInvoiceCommand(_companyId, invoice.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseErrorCodes.InvalidStatusTransition, result.Error!.Code);
        Assert.Empty(_stock.AddedGlEntries);
        Assert.Equal(PurchaseInvoiceStatus.Draft, invoice.Status);
        Assert.Equal(1100m, invoice.OutstandingAmount);
    }

    [Fact]
    public async Task Cancel_AlreadyCancelled_FailsWithInvoiceAlreadyCancelled()
    {
        var bill = await SeedInvoice(PurchaseInvoiceStatus.Cancelled, PostingDate);
        var invoice = bill.Invoice;

        var result = await Handler().HandleAsync(
            new CancelPurchaseInvoiceCommand(_companyId, invoice.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseErrorCodes.InvoiceAlreadyCancelled, result.Error!.Code);

        // Only the five seeded originals (3 invoice + 2 receipt) - no second reversal.
        Assert.Equal(5, _stock.AddedGlEntries.Count);
    }

    [Fact]
    public async Task Cancel_PaidInvoice_FailsWithInvalidStatusTransition()
    {
        // spec BY-05: payments must be refunded first, so Paid cannot go straight to Cancelled.
        var bill = await SeedInvoice(PurchaseInvoiceStatus.Paid, PostingDate, withGlRows: false);
        var invoice = bill.Invoice;

        var result = await Handler().HandleAsync(
            new CancelPurchaseInvoiceCommand(_companyId, invoice.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseErrorCodes.InvalidStatusTransition, result.Error!.Code);
        Assert.Empty(_stock.AddedGlEntries);
    }

    [Fact]
    public async Task Cancel_InvoiceWithoutLedgerRows_FailsWithInvoiceNotPosted()
    {
        var bill = await SeedInvoice(PurchaseInvoiceStatus.Unpaid, PostingDate, withGlRows: false);
        var invoice = bill.Invoice;

        var result = await Handler().HandleAsync(
            new CancelPurchaseInvoiceCommand(_companyId, invoice.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseErrorCodes.InvoiceNotPosted, result.Error!.Code);
        Assert.Empty(_stock.AddedGlEntries);

        // The outstanding balance is only zeroed AFTER the ledger rows are found, so a rejected
        // cancellation leaves it untouched (the status itself is rolled back by the posting
        // transaction in the real repository - the in-memory fake has no rollback to model it).
        Assert.Equal(1100m, invoice.OutstandingAmount);
    }

    [Fact]
    public async Task Cancel_FrozenOriginalPeriod_FailsWithFiscalPeriodLockedAndZeroLedgerRows()
    {
        // The reversal keeps the ORIGINAL PostingDate, so a frozen period blocks the
        // cancellation too ("posting, modification, or cancellation" - spec AC-04 wording).
        _companies.Company!.FrozenAccountsDate = FrozenThrough;
        var bill = await SeedInvoice(PurchaseInvoiceStatus.Unpaid, BackDated, withGlRows: false);
        var invoice = bill.Invoice;

        var result = await Handler().HandleAsync(
            new CancelPurchaseInvoiceCommand(_companyId, invoice.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(AccountingErrorCodes.FiscalPeriodLocked, result.Error!.Code);
        Assert.Empty(_stock.AddedGlEntries);
        Assert.Equal(PurchaseInvoiceStatus.Unpaid, invoice.Status);
    }

    [Fact]
    public async Task Cancel_StaleClientRowVersion_FailsWithConcurrencyConflict()
    {
        var bill = await SeedInvoice(PurchaseInvoiceStatus.Unpaid, PostingDate, withGlRows: false);
        var invoice = bill.Invoice;

        var result = await Handler().HandleAsync(
            new CancelPurchaseInvoiceCommand(_companyId, invoice.Id, new byte[] { 0xEE, 0xFF }));

        Assert.False(result.IsSuccess);
        Assert.Equal(ConcurrencyErrorCodes.ConcurrencyConflict, result.Error!.Code);
        Assert.Empty(_stock.AddedGlEntries);
        Assert.Equal(PurchaseInvoiceStatus.Unpaid, invoice.Status);
    }

    [Fact]
    public async Task Cancel_RaceOnInvoiceSave_FailsWithConcurrencyConflictAndNoReversal()
    {
        // The RowVersion WHERE clause matched 0 rows at SAVE time (nobody supplied a stale token,
        // the race happened between load and write). The reversal is written only AFTER the header
        // save, so the ledger keeps just the five ORIGINAL rows.
        var bill = await SeedInvoice(PurchaseInvoiceStatus.Unpaid, PostingDate);
        var invoice = bill.Invoice;
        _purchases.FailNextInvoiceUpdate = true;

        var result = await Handler().HandleAsync(
            new CancelPurchaseInvoiceCommand(_companyId, invoice.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(ConcurrencyErrorCodes.ConcurrencyConflict, result.Error!.Code);
        Assert.Equal(5, _stock.AddedGlEntries.Count);
        Assert.All(_stock.AddedGlEntries, r => Assert.False(r.IsCancelled));
    }

    [Fact]
    public async Task Cancel_UnknownInvoice_FailsWithInvoiceNotFound()
    {
        var result = await Handler().HandleAsync(
            new CancelPurchaseInvoiceCommand(_companyId, Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseErrorCodes.InvoiceNotFound, result.Error!.Code);
        Assert.Empty(_stock.AddedGlEntries);
    }

    [Fact]
    public async Task Cancel_InvoiceOfAnotherCompany_FailsWithInvoiceNotFound()
    {
        // Company mismatch is reported as NOT FOUND: the id must not leak across companies.
        var bill = await SeedInvoice(
            PurchaseInvoiceStatus.Unpaid, PostingDate, companyId: Guid.NewGuid(), withGlRows: false);
        var invoice = bill.Invoice;

        var result = await Handler().HandleAsync(
            new CancelPurchaseInvoiceCommand(_companyId, invoice.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseErrorCodes.InvoiceNotFound, result.Error!.Code);
        Assert.Empty(_stock.AddedGlEntries);
        Assert.Equal(PurchaseInvoiceStatus.Unpaid, invoice.Status);
    }

    // ------------------------------------------------- W1: the physical stock unwind (BY-05)

    [Fact]
    public async Task Cancel_AlreadyUnwoundReceipt_SkipsUnwindButStillCancelsInvoice()
    {
        var bill = await SeedInvoice(PurchaseInvoiceStatus.Unpaid, PostingDate);
        var invoice = bill.Invoice;

        // The receipt intake was already reversed by an earlier cancellation (shared receipt or a
        // retried unwind): its voucher carries a cancelled row next to the intake.
        _stock.SeedLedger(new StockLedgerEntry
        {
            Id = Guid.NewGuid(),
            TenantId = bill.Intake.TenantId,
            ItemId = bill.ItemId,
            WarehouseId = bill.WarehouseId,
            StockEntryId = null,
            VoucherType = ReceiptVoucherType,
            VoucherNo = bill.Receipt.VoucherNo,
            PostingDate = bill.Receipt.PostingDate,
            QtyChange = -bill.Intake.QtyChange,
            ValuationRate = bill.Intake.ValuationRate,
            Amount = -bill.Intake.Amount,
            CreatedAt = DateTimeOffset.UtcNow,
            IsCancelled = true,
        });

        var result = await Handler().HandleAsync(
            new CancelPurchaseInvoiceCommand(_companyId, invoice.Id));

        // The invoice itself still cancels with its own mirror...
        Assert.True(result.IsSuccess);
        Assert.Equal(PurchaseInvoiceStatus.Cancelled, invoice.Status);
        Assert.Equal(0m, invoice.OutstandingAmount);

        // ...but the receipt contributes NOTHING new: 5 seeded + 3 invoice reversals, no second
        // Kardex negation, no receipt GL mirror.
        Assert.Equal(8, _stock.AddedGlEntries.Count);
        Assert.All(
            _stock.AddedGlEntries.Where(r => r.IsCancelled),
            r => Assert.Equal(invoice.Id, r.VoucherId));
        Assert.Empty(_stock.AddedLedger);
    }

    [Fact]
    public async Task Cancel_FrozenReceiptPeriod_FailsWithFiscalPeriodLockedAndZeroWrites()
    {
        // The invoice itself is dated in the OPEN period, but the receipt behind it sits inside
        // the frozen one: the reversal would carry the RECEIPT's date, so the whole cancellation
        // is blocked all-or-nothing.
        _companies.Company!.FrozenAccountsDate = FrozenThrough;
        var bill = await SeedInvoice(
            PurchaseInvoiceStatus.Unpaid, PostingDate, receiptPostingDate: BackDated);
        var invoice = bill.Invoice;

        var result = await Handler().HandleAsync(
            new CancelPurchaseInvoiceCommand(_companyId, invoice.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(AccountingErrorCodes.FiscalPeriodLocked, result.Error!.Code);

        // Zero writes: the five seeded GL rows stay exactly that, no Kardex negation...
        Assert.Equal(5, _stock.AddedGlEntries.Count);
        Assert.All(_stock.AddedGlEntries, r => Assert.False(r.IsCancelled));
        Assert.Empty(_stock.AddedLedger);

        // ...and the bill is untouched (the production transaction rolls the status gate back;
        // the fake models that rollback).
        Assert.Equal(PurchaseInvoiceStatus.Unpaid, invoice.Status);
        Assert.Equal(1100m, invoice.OutstandingAmount);
    }

    [Fact]
    public async Task Cancel_MissingReceiptLine_FailsWithPurchaseReceiptNotFoundAndZeroWrites()
    {
        var bill = await SeedInvoice(PurchaseInvoiceStatus.Unpaid, PostingDate);
        var invoice = bill.Invoice;

        // The billed receipt line does not exist in this tenant (deleted out of band).
        invoice.Lines.Single().PurchaseReceiptLineId = Guid.NewGuid();

        var result = await Handler().HandleAsync(
            new CancelPurchaseInvoiceCommand(_companyId, invoice.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseErrorCodes.PurchaseReceiptNotFound, result.Error!.Code);
        Assert.Equal(5, _stock.AddedGlEntries.Count);
        Assert.Empty(_stock.AddedLedger);
        Assert.Equal(PurchaseInvoiceStatus.Unpaid, invoice.Status);
        Assert.Equal(1100m, invoice.OutstandingAmount);
    }

    [Fact]
    public async Task Cancel_MissingReceiptHeader_FailsWithPurchaseReceiptNotFoundAndZeroWrites()
    {
        var bill = await SeedInvoice(PurchaseInvoiceStatus.Unpaid, PostingDate);
        var invoice = bill.Invoice;

        // The line exists, but its parent receipt header is gone (points at an unknown id).
        bill.Receipt.Lines.Single().PurchaseReceiptId = Guid.NewGuid();

        var result = await Handler().HandleAsync(
            new CancelPurchaseInvoiceCommand(_companyId, invoice.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseErrorCodes.PurchaseReceiptNotFound, result.Error!.Code);
        Assert.Equal(5, _stock.AddedGlEntries.Count);
        Assert.Empty(_stock.AddedLedger);
        Assert.Equal(PurchaseInvoiceStatus.Unpaid, invoice.Status);
    }

    [Fact]
    public async Task Cancel_ReceiptOfAnotherCompany_FailsWithPurchaseReceiptNotFound()
    {
        // Cross-company linkage is reported as NOT FOUND: the id must not leak across companies.
        var bill = await SeedInvoice(
            PurchaseInvoiceStatus.Unpaid, PostingDate, receiptCompanyId: Guid.NewGuid());
        var invoice = bill.Invoice;

        var result = await Handler().HandleAsync(
            new CancelPurchaseInvoiceCommand(_companyId, invoice.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseErrorCodes.PurchaseReceiptNotFound, result.Error!.Code);
        Assert.Equal(5, _stock.AddedGlEntries.Count);
        Assert.Empty(_stock.AddedLedger);
        Assert.Equal(PurchaseInvoiceStatus.Unpaid, invoice.Status);
    }

    [Fact]
    public async Task Cancel_InvoiceWithoutReceiptLinkage_KeepsExistingBehavior()
    {
        // A legacy bill with no receipt lines: there is nothing physical to unwind, so the
        // cancellation is exactly the pre-W1 invoice mirror.
        var bill = await SeedInvoice(PurchaseInvoiceStatus.Unpaid, PostingDate);
        var invoice = bill.Invoice;
        invoice.Lines.Clear();

        var result = await Handler().HandleAsync(
            new CancelPurchaseInvoiceCommand(_companyId, invoice.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(PurchaseInvoiceStatus.Cancelled, invoice.Status);
        Assert.Equal(0m, invoice.OutstandingAmount);

        // 5 seeded + 3 invoice reversals only; no Kardex row, no receipt mirror.
        Assert.Equal(8, _stock.AddedGlEntries.Count);
        Assert.Empty(_stock.AddedLedger);
    }
}
