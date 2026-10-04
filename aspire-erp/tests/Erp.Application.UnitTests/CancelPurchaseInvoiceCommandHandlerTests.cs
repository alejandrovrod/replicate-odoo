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
    /// Seeds a posted bill (optionally with its ledger rows) - cancellation tests start from a
    /// persisted aggregate, never from the posting handler.
    /// </summary>
    private async Task<PurchaseInvoice> SeedInvoice(
        PurchaseInvoiceStatus status,
        DateOnly postingDate,
        Guid? companyId = null,
        byte[]? rowVersion = null,
        bool withGlRows = true)
    {
        var invoice = new PurchaseInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = companyId ?? _companyId,
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
                    ItemId = Guid.NewGuid(),
                    PurchaseReceiptLineId = Guid.NewGuid(),
                    Qty = 10m,
                    Rate = 100m,
                    Amount = 1000m,
                    LineNumber = 1,
                },
            },
        };

        _purchases.SeedInvoice(invoice);

        if (withGlRows)
        {
            // BY-01's canonical triple: Dr 2120 1000 / Dr 1130 100 / Cr 2110 1100.
            await _stock.AddGlEntriesAsync(new[]
            {
                NewGlRow(invoice, accountCode: "2120", debit: 1000m, credit: 0m),
                NewGlRow(invoice, accountCode: "1130", debit: 100m, credit: 0m),
                NewGlRow(invoice, accountCode: "2110", debit: 0m, credit: 1100m),
            });
        }

        return invoice;
    }

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

    // ------------------------------------------------------------------ happy path (spec BY-05)

    [Fact]
    public async Task Cancel_UnpaidInvoice_AppendsSwappedReversalAndKeepsOriginals()
    {
        var invoice = await SeedInvoice(PurchaseInvoiceStatus.Unpaid, PostingDate);

        // Snapshot of the ORIGINALS taken before the cancellation: the append-only guarantee
        // (Constitution III.2) means these values must be untouched afterwards.
        var originalsBefore = _stock.AddedGlEntries
            .Select(r => (r.Debit, r.Credit, r.IsCancelled, r.Remarks))
            .ToList();

        var result = await Handler().HandleAsync(
            new CancelPurchaseInvoiceCommand(_companyId, invoice.Id));

        // spec BY-05: status transition + zeroed outstanding balance.
        Assert.True(result.IsSuccess);
        Assert.Equal(PurchaseInvoiceStatus.Cancelled, result.Value!.Status);
        Assert.Equal(0m, result.Value.OutstandingAmount);
        Assert.Equal(PurchaseInvoiceStatus.Cancelled, invoice.Status);
        Assert.Equal(0m, invoice.OutstandingAmount);
        Assert.Equal(1, _purchases.TransactionCount);

        // Three originals + three reversal rows.
        Assert.Equal(6, _stock.AddedGlEntries.Count);
        var reversals = _stock.AddedGlEntries.Skip(3).ToList();
        Assert.Equal(3, reversals.Count);

        foreach (var original in _stock.AddedGlEntries.Take(3))
        {
            var reversal = Assert.Single(reversals, r => r.AccountId == original.AccountId);

            // The whole point of the compensating entry: sides SWAPPED, same voucher identity.
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

        // ...and the originals themselves were NEVER mutated.
        Assert.Equal(
            originalsBefore,
            _stock.AddedGlEntries.Take(3).Select(r => (r.Debit, r.Credit, r.IsCancelled, r.Remarks)));

        // Trial-balance neutrality: debits == credits across originals + reversals.
        Assert.Equal(
            _stock.AddedGlEntries.Sum(r => r.Debit),
            _stock.AddedGlEntries.Sum(r => r.Credit));
    }

    [Fact]
    public async Task Cancel_PartiallyPaidInvoice_CancelsAndZeroesOutstanding()
    {
        var invoice = await SeedInvoice(PurchaseInvoiceStatus.PartiallyPaid, PostingDate);

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
        var invoice = await SeedInvoice(PurchaseInvoiceStatus.Draft, PostingDate, withGlRows: false);

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
        var invoice = await SeedInvoice(PurchaseInvoiceStatus.Cancelled, PostingDate);

        var result = await Handler().HandleAsync(
            new CancelPurchaseInvoiceCommand(_companyId, invoice.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseErrorCodes.InvoiceAlreadyCancelled, result.Error!.Code);

        // Only the three seeded originals - no second reversal was appended.
        Assert.Equal(3, _stock.AddedGlEntries.Count);
    }

    [Fact]
    public async Task Cancel_PaidInvoice_FailsWithInvalidStatusTransition()
    {
        // spec BY-05: payments must be refunded first, so Paid cannot go straight to Cancelled.
        var invoice = await SeedInvoice(PurchaseInvoiceStatus.Paid, PostingDate, withGlRows: false);

        var result = await Handler().HandleAsync(
            new CancelPurchaseInvoiceCommand(_companyId, invoice.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseErrorCodes.InvalidStatusTransition, result.Error!.Code);
        Assert.Empty(_stock.AddedGlEntries);
    }

    [Fact]
    public async Task Cancel_InvoiceWithoutLedgerRows_FailsWithInvoiceNotPosted()
    {
        var invoice = await SeedInvoice(PurchaseInvoiceStatus.Unpaid, PostingDate, withGlRows: false);

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
        var invoice = await SeedInvoice(PurchaseInvoiceStatus.Unpaid, BackDated, withGlRows: false);

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
        var invoice = await SeedInvoice(PurchaseInvoiceStatus.Unpaid, PostingDate, withGlRows: false);

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
        // save, so the ledger keeps just the three ORIGINAL rows.
        var invoice = await SeedInvoice(PurchaseInvoiceStatus.Unpaid, PostingDate);
        _purchases.FailNextInvoiceUpdate = true;

        var result = await Handler().HandleAsync(
            new CancelPurchaseInvoiceCommand(_companyId, invoice.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(ConcurrencyErrorCodes.ConcurrencyConflict, result.Error!.Code);
        Assert.Equal(3, _stock.AddedGlEntries.Count);
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
        var invoice = await SeedInvoice(
            PurchaseInvoiceStatus.Unpaid, PostingDate, companyId: Guid.NewGuid(), withGlRows: false);

        var result = await Handler().HandleAsync(
            new CancelPurchaseInvoiceCommand(_companyId, invoice.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseErrorCodes.InvoiceNotFound, result.Error!.Code);
        Assert.Empty(_stock.AddedGlEntries);
        Assert.Equal(PurchaseInvoiceStatus.Unpaid, invoice.Status);
    }
}
