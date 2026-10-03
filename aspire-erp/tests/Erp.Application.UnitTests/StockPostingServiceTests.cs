using Erp.Application.Services;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Task 3.2/D12: <see cref="StockPostingService"/> orchestration - ST-01/ST-02 GL mapping, FIFO
/// consumption, gapless voucher prefixes, configuration failures - against in-memory repository
/// doubles, so no EF Core and no database are involved (Constitution I.2/I.3).
/// </summary>
public sealed class StockPostingServiceTests
{
    private static readonly DateOnly PostingDate = new(2026, 3, 2);

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeWarehouseRepository _warehouses = new();
    private readonly FakeItemRepository _items = new();
    private readonly FakeStockRepository _stock = new();

    private readonly Account _stockAccount;   // 1310 - Stock In Hand (warehouse asset)
    private readonly Account _receivedAccount; // 2120 - Stock Received But Not Billed
    private readonly Account _expenseAccount;  // 5210 - Cost of Goods Sold
    private readonly Warehouse _warehouse;
    private readonly Item _item;

    public StockPostingServiceTests()
    {
        _stockAccount = NewAccount("1310", "Stock In Hand");
        _receivedAccount = NewAccount("2120", "Stock Received But Not Billed");
        _expenseAccount = NewAccount("5210", "Cost of Goods Sold");
        _accounts.Seed(_stockAccount, _receivedAccount, _expenseAccount);
        _accounts.AccountsByCode = new[] { _receivedAccount };

        _warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = _companyId,
            ItemCode = "WH-01",
            ItemName = "Main Stores",
            AccountId = _stockAccount.Id,
        };
        _warehouses.Seed(_warehouse);

        _item = new Item
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ItemCode = "IT-001",
            ItemName = "Steel Bracket",
            ValuationMethod = ValuationMethod.Fifo,
            StockUomId = Guid.NewGuid(),
            
        };
        _items.Seed(_item);

        _companies.Company = new Company
        {
            Id = _companyId,
            TenantId = _tenantId,
            ItemName = "Acme Holding",
            AllowNegativeStock = false,
            StockReceivedAccountCode = "2120",
        };
    }

    private Account NewAccount(string code, string name) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = _companyId,
            AccountCode = code,
            AccountName = name,
            IsActive = true,
            IsGroup = false,
        };

    private StockPostingService CreateService() =>
        new(_companies, _accounts, _warehouses, _items, _stock);

    private StockPostingRequest Request(
        StockEntryType entryType,
        IReadOnlyList<StockPostingLine>? lines = null,
        Guid? targetWarehouseId = null) =>
        new(
            _companyId,
            entryType,
            PostingDate,
            _warehouse.Id,
            targetWarehouseId,
            lines ?? new List<StockPostingLine>());

    private void SeedFifoLayers()
    {
        // The two ST-02 receipts: 50 @ $10.00 (day -2) and 10 @ $12.00 (day -1).
        _stock.SeedLedger(
            LedgerRow(+50m, 10m, PostingDate.AddDays(-2)),
            LedgerRow(+10m, 12m, PostingDate.AddDays(-1)));
    }

    private StockLedgerEntry LedgerRow(decimal qtyChange, decimal rate, DateOnly postingDate) =>
        new()
        {
            Id = Guid.NewGuid(),
            ItemId = _item.Id,
            WarehouseId = _warehouse.Id,
            PostingDate = postingDate,
            QtyChange = qtyChange,
            ValuationRate = rate,
            Amount = qtyChange * rate,
            CreatedAt = new DateTimeOffset(postingDate, TimeOnly.MinValue, TimeSpan.Zero),
        };

    // ------------------------------------------------------------------------- happy paths

    [Fact]
    public async Task PostAsync_Receipt_WritesBalancedSt01GeneralLedgerLines()
    {
        // spec §4 ST-01: receipt 60 @ $10.00 -> Debit 1310 Stock In Hand / Credit 2120.
        var request = Request(
            StockEntryType.MaterialReceipt,
            new List<StockPostingLine> { new(_item.Id, 60m, 10.00m) });

        var result = await CreateService().PostAsync(request);

        Assert.Equal("MR-2026-00001", result.Entry.VoucherNo);
        Assert.Equal(2, result.GlEntries.Count);
        Assert.Equal(result.TotalDebit, result.TotalCredit);
        Assert.Equal(600.00m, result.TotalDebit);

        var debit = Assert.Single(result.GlEntries, g => g.Debit > 0);
        var credit = Assert.Single(result.GlEntries, g => g.Credit > 0);
        Assert.Equal("1310", debit.AccountCode);
        Assert.Equal(600.00m, debit.Debit);
        Assert.Equal("2120", credit.AccountCode);
        Assert.Equal(600.00m, credit.Credit);
        Assert.Equal("StockEntry", debit.VoucherType);

        var sle = Assert.Single(result.LedgerEntries);
        Assert.Equal(60m, sle.QtyChange);
        Assert.Equal(10.00m, sle.ValuationRate);
        Assert.Equal(600.00m, sle.Amount);

        // All three aggregates persisted inside ONE transaction.
        Assert.Equal(1, _stock.TransactionCount);
        var entry = Assert.Single(_stock.StockEntries);
        Assert.Equal("MR-2026-00001", entry.VoucherNo);
        Assert.NotEqual(default, entry.CreatedAt);
        Assert.Equal(2, _stock.AddedGlEntries.Count);
    }

    [Fact]
    public async Task PostAsync_Issue_St02FifoCostsExactly620()
    {
        // spec §4 ST-02: 50 @ $10 + 10 @ $12 in stock, issue 60 -> COGS $620.00.
        SeedFifoLayers();

        var request = Request(
            StockEntryType.MaterialIssue,
            new List<StockPostingLine> { new(_item.Id, 60m, null) });

        var result = await CreateService().PostAsync(request);

        Assert.Equal("MI-2026-00001", result.Entry.VoucherNo);
        Assert.Equal(620.00m, result.TotalDebit);
        Assert.Equal(620.00m, result.TotalCredit);

        var debit = Assert.Single(result.GlEntries, g => g.Debit > 0);
        var credit = Assert.Single(result.GlEntries, g => g.Credit > 0);
        Assert.Equal("5210", debit.AccountCode);
        Assert.Equal(620.00m, debit.Debit);
        Assert.Equal("1310", credit.AccountCode);
        Assert.Equal(620.00m, credit.Credit);

        var sle = Assert.Single(result.LedgerEntries);
        Assert.Equal(-60m, sle.QtyChange);
        Assert.Equal(-620.00m, sle.Amount);
        Assert.Equal(10.333333m, sle.ValuationRate);
    }

    [Fact]
    public async Task PostAsync_TransferBetweenSameStockAccount_PreservesValueWithoutGlLines()
    {
        SeedFifoLayers();

        var target = new Warehouse
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = _companyId,
            ItemCode = "WH-02",
            ItemName = "Branch Stores",
            AccountId = _stockAccount.Id, // SAME GL account
        };
        _warehouses.Seed(target);

        var request = Request(
            StockEntryType.MaterialTransfer,
            new List<StockPostingLine> { new(_item.Id, 20m, null) },
            targetWarehouseId: target.Id);

        var result = await CreateService().PostAsync(request);

        Assert.Equal("MT-2026-00001", result.Entry.VoucherNo);
        Assert.Empty(result.GlEntries); // value never leaves the single stock account
        Assert.Equal(0m, result.TotalDebit);
        Assert.Equal(0m, result.TotalCredit);

        Assert.Equal(2, result.LedgerEntries.Count);
        Assert.Equal(-20m, result.LedgerEntries[0].QtyChange);
        Assert.Equal(+20m, result.LedgerEntries[1].QtyChange);
        Assert.Equal(
            result.LedgerEntries[0].Amount,
            -result.LedgerEntries[1].Amount);
    }

    [Fact]
    public async Task PostAsync_TransferBetweenDifferentStockAccounts_BalancesBothAccounts()
    {
        SeedFifoLayers();

        var otherStockAccount = NewAccount("1320", "Stock In Hand - Branch");
        _accounts.Seed(otherStockAccount);

        var target = new Warehouse
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = _companyId,
            ItemCode = "WH-03",
            ItemName = "External Store",
            AccountId = otherStockAccount.Id,
        };
        _warehouses.Seed(target);

        var request = Request(
            StockEntryType.MaterialTransfer,
            new List<StockPostingLine> { new(_item.Id, 5m, null) },
            targetWarehouseId: target.Id);

        var result = await CreateService().PostAsync(request);

        Assert.Equal(2, result.GlEntries.Count);
        Assert.Equal(result.TotalDebit, result.TotalCredit);
        // FIFO eats the OLDEST layer first: 5 units from the 50 @ $10 layer -> $50.00
        // (the $12 layer stays untouched).
        Assert.Equal(50.00m, result.TotalDebit);
    }

    [Fact]
    public async Task PostAsync_VoucherNumbers_AreGaplessAndPrefixedPerEntryType()
    {
        var receipt = Request(
            StockEntryType.MaterialReceipt,
            new List<StockPostingLine> { new(_item.Id, 5m, 2m) });

        var service = CreateService();
        var first = await service.PostAsync(receipt);
        var second = await service.PostAsync(receipt);
        var third = await service.PostAsync(receipt);

        Assert.Equal("MR-2026-00001", first.Entry.VoucherNo);
        Assert.Equal("MR-2026-00002", second.Entry.VoucherNo);
        Assert.Equal("MR-2026-00003", third.Entry.VoucherNo);
    }

    // ------------------------------------------------------------------ negative stock (3.3)

    [Fact]
    public async Task PostAsync_IssueBeyondStock_WhenPolicyForbidsNegatives_ThrowsInsufficientStock()
    {
        SeedFifoLayers(); // 60 units on hand

        var request = Request(
            StockEntryType.MaterialIssue,
            new List<StockPostingLine> { new(_item.Id, 61m, null) });

        var ex = await Assert.ThrowsAsync<InsufficientStockException>(
            () => CreateService().PostAsync(request));

        Assert.Equal(StockErrorCodes.InsufficientStock, ex.Code);
        Assert.Equal("IT-001", ex.ItemCode);
        Assert.Equal("WH-01", ex.WarehouseCode);
        Assert.Equal(60m, ex.Available);
        Assert.Equal(61m, ex.Requested);

        // Task 3.3 evidence: a rejected posting writes NOTHING.
        Assert.Empty(_stock.StockEntries);
        Assert.Empty(_stock.AddedLedger);
        Assert.Empty(_stock.AddedGlEntries);
    }

    [Fact]
    public async Task PostAsync_IssueBeyondStock_WhenPolicyAllowsNegatives_PostsTheShortfall()
    {
        _companies.Company!.AllowNegativeStock = true;
        SeedFifoLayers(); // 60 units on hand

        var request = Request(
            StockEntryType.MaterialIssue,
            new List<StockPostingLine> { new(_item.Id, 65m, null) });

        var result = await CreateService().PostAsync(request);

        // 50 @ $10 + 10 @ $12 + shortfall 5 @ $12 = 500 + 120 + 60 = 680.00
        Assert.Equal(680.00m, result.TotalDebit);
        Assert.Equal(680.00m, result.TotalCredit);
        var sle = Assert.Single(result.LedgerEntries);
        Assert.Equal(-65m, sle.QtyChange);
    }

    // ------------------------------------------------- fiscal period lock (task 2.2 / AC-04)

    [Fact]
    public async Task PostAsync_BackDatedAgainstFrozenCompany_ThrowsFiscalPeriodLockAndWritesNothing()
    {
        // spec AC-04 Gherkin: freeze date 2025-12-31, attempt posts on 2025-12-15.
        _companies.Company!.FrozenAccountsDate = new DateOnly(2025, 12, 31);

        var request = Request(
                StockEntryType.MaterialReceipt,
                new List<StockPostingLine> { new(_item.Id, 10m, 5m) })
            with { PostingDate = new DateOnly(2025, 12, 15) };

        var ex = await Assert.ThrowsAsync<FiscalPeriodLockedException>(
            () => CreateService().PostAsync(request));

        Assert.Equal(AccountingErrorCodes.FiscalPeriodLocked, ex.Code);
        Assert.Equal(new DateOnly(2025, 12, 15), ex.PostingDate);
        Assert.Equal(new DateOnly(2025, 12, 31), ex.FrozenAccountsDate);

        // AC-04: "no data is modified" - the domain check runs before the FIRST line is built.
        Assert.Empty(_stock.StockEntries);
        Assert.Empty(_stock.AddedLedger);
        Assert.Empty(_stock.AddedGlEntries);
    }

    [Fact]
    public async Task PostAsync_AllPostings_SatisfyDoubleEntryZeroSumInvariant()
    {
        // Task 2.1 acceptance AT THE SERVICE LEVEL: whatever these services emit obeys
        // spec AC-01 |sum D - sum C| <= 0.0001. The THROW side lives in DoubleEntryGuardTests;
        // an imbalanced line set is structurally unreachable through this service because both
        // sides of every line pair derive from ONE rounded amount (the guard stays as
        // defense-in-depth for the user-authored journal lines of tasks.md 2.3).
        SeedFifoLayers();

        var service = CreateService();
        var receipt = await service.PostAsync(
            Request(StockEntryType.MaterialReceipt, new List<StockPostingLine> { new(_item.Id, 60m, 10.00m) }));
        var issue = await service.PostAsync(
            Request(StockEntryType.MaterialIssue, new List<StockPostingLine> { new(_item.Id, 25m, null) }));

        foreach (var posting in new[] { receipt, issue })
        {
            Assert.True(
                Math.Abs(posting.TotalDebit - posting.TotalCredit) <= 0.0001m,
                $"Voucher {posting.Entry.VoucherNo} is out of balance: "
                + $"D={posting.TotalDebit:0.0000}, C={posting.TotalCredit:0.0000}.");
        }
    }

    // ------------------------------------------------------------------ validation failures

    [Fact]
    public async Task PostAsync_NoLines_ThrowsNoLinesBeforeAnyTransaction()
    {
        var ex = await Assert.ThrowsAsync<StockValidationException>(
            () => CreateService().PostAsync(Request(StockEntryType.MaterialReceipt)));

        Assert.Equal(StockErrorCodes.NoLines, ex.Code);
        Assert.Equal(0, _stock.TransactionCount);
    }

    [Fact]
    public async Task PostAsync_UnknownCompany_ThrowsCompanyNotFound()
    {
        _companies.Company = null; // tenant has no such company

        var request = Request(
            StockEntryType.MaterialReceipt,
            new List<StockPostingLine> { new(_item.Id, 1m, 1m) });

        var ex = await Assert.ThrowsAsync<StockValidationException>(
            () => CreateService().PostAsync(request));

        Assert.Equal(StockErrorCodes.CompanyNotFound, ex.Code);
    }

    [Fact]
    public async Task PostAsync_WarehouseBelongingToAnotherCompany_ThrowsWarehouseNotFound()
    {
        // The warehouse exists but is owned by a different company: the posting must refuse it.
        var request = Request(
            StockEntryType.MaterialReceipt,
            new List<StockPostingLine> { new(_item.Id, 1m, 1m) }) with { CompanyId = Guid.NewGuid() };

        var ex = await Assert.ThrowsAsync<StockValidationException>(
            () => CreateService().PostAsync(request));

        Assert.Equal(StockErrorCodes.WarehouseNotFound, ex.Code);
    }

    [Fact]
    public async Task PostAsync_UnknownWarehouse_ThrowsWarehouseNotFound()
    {
        var request = Request(
            StockEntryType.MaterialReceipt,
            new List<StockPostingLine> { new(_item.Id, 1m, 1m) }) with { WarehouseId = Guid.NewGuid() };

        var ex = await Assert.ThrowsAsync<StockValidationException>(
            () => CreateService().PostAsync(request));

        Assert.Equal(StockErrorCodes.WarehouseNotFound, ex.Code);
    }

    [Fact]
    public async Task PostAsync_ReceiptWithoutRate_ThrowsInvalidRate()
    {
        var request = Request(
            StockEntryType.MaterialReceipt,
            new List<StockPostingLine> { new(_item.Id, 10m, null) });

        var ex = await Assert.ThrowsAsync<StockValidationException>(
            () => CreateService().PostAsync(request));

        Assert.Equal(StockErrorCodes.InvalidRate, ex.Code);
    }

    [Fact]
    public async Task PostAsync_NonPositiveQuantity_ThrowsInvalidQuantity()
    {
        var request = Request(
            StockEntryType.MaterialReceipt,
            new List<StockPostingLine> { new(_item.Id, 0m, 1m) });

        var ex = await Assert.ThrowsAsync<StockValidationException>(
            () => CreateService().PostAsync(request));

        Assert.Equal(StockErrorCodes.InvalidQuantity, ex.Code);
    }

    [Fact]
    public async Task PostAsync_TransferWithoutTarget_ThrowsInvalidTargetWarehouse()
    {
        SeedFifoLayers();

        var request = Request(
            StockEntryType.MaterialTransfer,
            new List<StockPostingLine> { new(_item.Id, 1m, null) });

        var ex = await Assert.ThrowsAsync<StockValidationException>(
            () => CreateService().PostAsync(request));

        Assert.Equal(StockErrorCodes.InvalidTargetWarehouse, ex.Code);
    }

    [Fact]
    public async Task PostAsync_TargetWarehouseOnReceipt_ThrowsInvalidTargetWarehouse()
    {
        var other = new Warehouse
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = _companyId,
            ItemCode = "WH-09",
            ItemName = "Other",
            AccountId = _stockAccount.Id,
        };
        _warehouses.Seed(other);

        var request = Request(
            StockEntryType.MaterialReceipt,
            new List<StockPostingLine> { new(_item.Id, 1m, 1m) },
            targetWarehouseId: other.Id);

        var ex = await Assert.ThrowsAsync<StockValidationException>(
            () => CreateService().PostAsync(request));

        Assert.Equal(StockErrorCodes.InvalidTargetWarehouse, ex.Code);
    }

    // ----------------------------------------------------------------- configuration failures

    [Fact]
    public async Task PostAsync_CompanyWithoutStockReceivedCode_ThrowsConfigurationException()
    {
        _companies.Company!.StockReceivedAccountCode = null;

        var request = Request(
            StockEntryType.MaterialReceipt,
            new List<StockPostingLine> { new(_item.Id, 1m, 1m) });

        var ex = await Assert.ThrowsAsync<StockPostingConfigurationException>(
            () => CreateService().PostAsync(request));

        Assert.Contains("StockReceivedAccountCode", ex.Message, StringComparison.Ordinal);
        Assert.Empty(_stock.StockEntries);
    }

    [Fact]
    public async Task PostAsync_WarehouseWithoutStockAccount_ThrowsConfigurationException()
    {
        _warehouse.AccountId = Guid.Empty;

        var request = Request(
            StockEntryType.MaterialReceipt,
            new List<StockPostingLine> { new(_item.Id, 1m, 1m) });

        var ex = await Assert.ThrowsAsync<StockPostingConfigurationException>(
            () => CreateService().PostAsync(request));

        Assert.Contains("stock account", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostAsync_GroupStockAccount_ThrowsInvalidGlAccount()
    {
        _stockAccount.IsGroup = true; // III.3: group accounts cannot receive postings

        var request = Request(
            StockEntryType.MaterialReceipt,
            new List<StockPostingLine> { new(_item.Id, 1m, 1m) });

        var ex = await Assert.ThrowsAsync<StockValidationException>(
            () => CreateService().PostAsync(request));

        Assert.Equal(StockErrorCodes.InvalidGlAccount, ex.Code);
    }

    [Fact]
    public async Task PostAsync_InactiveStockAccount_ThrowsInvalidGlAccount()
    {
        _stockAccount.IsActive = false;

        var request = Request(
            StockEntryType.MaterialReceipt,
            new List<StockPostingLine> { new(_item.Id, 1m, 1m) });

        var ex = await Assert.ThrowsAsync<StockValidationException>(
            () => CreateService().PostAsync(request));

        Assert.Equal(StockErrorCodes.InvalidGlAccount, ex.Code);
    }



    [Fact]
    public async Task PostAsync_NonFifoValuationMethod_IsNotSupportedYet()
    {
        _item.ValuationMethod = ValuationMethod.MovingAverage;
        SeedFifoLayers();

        var request = Request(
            StockEntryType.MaterialIssue,
            new List<StockPostingLine> { new(_item.Id, 1m, null) });

        await Assert.ThrowsAsync<NotSupportedException>(() => CreateService().PostAsync(request));
    }

    [Fact]
    public async Task PostAsync_UnknownItemOnLine_ThrowsItemNotFound()
    {
        var request = Request(
            StockEntryType.MaterialReceipt,
            new List<StockPostingLine> { new(Guid.NewGuid(), 1m, 1m) });

        var ex = await Assert.ThrowsAsync<StockValidationException>(
            () => CreateService().PostAsync(request));

        Assert.Equal(StockErrorCodes.ItemNotFound, ex.Code);
    }

    [Fact]
    public async Task PostAsync_NullRequest_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateService().PostAsync(null!));
    }
}




