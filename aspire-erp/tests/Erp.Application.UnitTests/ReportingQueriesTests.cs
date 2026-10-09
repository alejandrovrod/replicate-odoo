using Erp.Application.Features.GeneralLedger.Queries;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// tasks.md 7.1 - the two new reporting reads through their CQRS handlers against in-memory
/// doubles: the Kardex report (chronological movements, per-pair opening rows and running
/// balances) and the AR/AP aging (open invoices only, ERPNext due-date buckets and totals).
/// </summary>
public sealed class ReportingQueriesTests
{
    private readonly Guid _companyId = Guid.NewGuid();

    // ---------------------------------------------------------------------------------------------
    // GET stock-ledger
    // ---------------------------------------------------------------------------------------------

    private static StockLedgerEntry KardexRow(
        Guid itemId,
        Guid warehouseId,
        DateOnly date,
        decimal qty,
        decimal amount,
        decimal rate,
        string voucherNo,
        Item? item = null,
        Warehouse? warehouse = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            ItemId = itemId,
            Item = item,
            WarehouseId = warehouseId,
            Warehouse = warehouse,
            PostingDate = date,
            QtyChange = qty,
            Amount = amount,
            ValuationRate = rate,
            VoucherType = "StockEntry",
            VoucherNo = voucherNo,
            CreatedAt = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
        };

    [Fact]
    public async Task StockLedger_MovementsCarryOpeningRowAndRunningBalances()
    {
        var stock = new FakeStockRepository();
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var item = new Item { Id = itemId, ItemCode = "IT-001", ItemName = "Steel Bracket" };
        var warehouse = new Warehouse { Id = warehouseId, WarehouseCode = "WH-01", WarehouseName = "Main" };
        stock.SeedLedger(
            KardexRow(itemId, warehouseId, new DateOnly(2026, 1, 5), 10m, 1000m, 100m, "MR-2026-00001", item, warehouse),
            KardexRow(itemId, warehouseId, new DateOnly(2026, 2, 10), 5m, 550m, 110m, "MR-2026-00002", item, warehouse),
            KardexRow(itemId, warehouseId, new DateOnly(2026, 3, 2), -4m, -440m, 110m, "MI-2026-00001", item, warehouse));

        var report = await new GetStockLedgerReportQueryHandler(stock).HandleAsync(
            new GetStockLedgerReportQuery(_companyId, new DateOnly(2026, 2, 1), new DateOnly(2026, 3, 31)));

        // Opening row (pre-period 10 units @ 100) + two period movements.
        Assert.Equal(3, report.Rows.Count);
        var opening = report.Rows[0];
        Assert.True(opening.IsOpening);
        Assert.Equal(10m, opening.BalanceQty);
        Assert.Equal(1000m, opening.BalanceValue);

        Assert.Equal(15m, report.Rows[1].BalanceQty);
        Assert.Equal(1550m, report.Rows[1].BalanceValue);
        Assert.Equal(5m, report.Rows[1].InQty);
        Assert.Equal(0m, report.Rows[1].OutQty);

        Assert.Equal(11m, report.Rows[2].BalanceQty);
        Assert.Equal(1110m, report.Rows[2].BalanceValue);
        Assert.Equal(-4m, report.Rows[2].OutQty);

        Assert.Equal(5m, report.TotalInQty);
        Assert.Equal(-4m, report.TotalOutQty);
        Assert.Equal(110m, report.TotalValueChange);
    }

    [Fact]
    public async Task StockLedger_EmptyPeriod_ReturnsEmptyRowsAndZeroTotals()
    {
        var report = await new GetStockLedgerReportQueryHandler(new FakeStockRepository()).HandleAsync(
            new GetStockLedgerReportQuery(_companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)));

        Assert.Empty(report.Rows);
        Assert.Equal(0m, report.TotalInQty);
        Assert.Equal(0m, report.TotalValueChange);
    }

    // ---------------------------------------------------------------------------------------------
    // GET aging
    // ---------------------------------------------------------------------------------------------

    private static SalesInvoice Receivable(
        Guid companyId,
        Guid customerId,
        string number,
        DateOnly posting,
        DateOnly due,
        decimal grand,
        decimal outstanding,
        SalesInvoiceStatus status,
        string customerName = "ACME") =>
        new()
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            InvoiceNumber = number,
            CustomerId = customerId,
            Customer = new Customer { Id = customerId, CompanyId = companyId, CustomerCode = "C", CustomerName = customerName },
            PostingDate = posting,
            DueDate = due,
            Status = status,
            GrandTotal = grand,
            OutstandingAmount = outstanding,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private static PurchaseInvoice Payable(
        Guid companyId,
        Guid supplierId,
        string number,
        DateOnly posting,
        DateOnly due,
        decimal grand,
        decimal outstanding,
        PurchaseInvoiceStatus status) =>
        new()
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            BillNumber = number,
            SupplierId = supplierId,
            Supplier = new Supplier { Id = supplierId, Code = "S", Name = "Globex" },
            PostingDate = posting,
            DueDate = due,
            Status = status,
            GrandTotal = grand,
            OutstandingAmount = outstanding,
        };

    [Fact]
    public async Task Aging_OpenInvoicesBucketed_PaidAndDraftExcluded()
    {
        var sales = new FakeSalesInvoiceRepository();
        var purchases = new FakePurchaseRepository();
        var customerId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var anchor = new DateOnly(2026, 10, 10);

        sales.Seed(
            Receivable(_companyId, customerId, "SINV-001", new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 5), 100m, 100m, SalesInvoiceStatus.Unpaid),
            Receivable(_companyId, customerId, "SINV-002", new DateOnly(2026, 7, 1), new DateOnly(2026, 8, 1), 200m, 50m, SalesInvoiceStatus.PartiallyPaid),
            Receivable(_companyId, customerId, "SINV-003", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 20), 300m, 0m, SalesInvoiceStatus.Paid),
            Receivable(_companyId, customerId, "SINV-004", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 20), 400m, 400m, SalesInvoiceStatus.Draft));
        purchases.SeedInvoice(
            Payable(_companyId, supplierId, "PINV-001", new DateOnly(2026, 9, 15), new DateOnly(2026, 10, 20), 500m, 500m, PurchaseInvoiceStatus.Unpaid));

        var report = await new GetAgingReportQueryHandler(sales, purchases).HandleAsync(
            new GetAgingReportQuery(_companyId, anchor));

        // Only the two open receivables + the open payable (paid + draft excluded).
        Assert.Equal(3, report.Rows.Count);

        var r1 = report.Rows.First(r => r.VoucherNo == "SINV-001");
        Assert.Equal(5, r1.AgeDays);
        Assert.Equal("Range030", r1.Bucket);
        Assert.Equal(100m, r1.InvoicedAmount);
        Assert.Equal(0m, r1.PaidAmount);

        var r2 = report.Rows.First(r => r.VoucherNo == "SINV-002");
        Assert.Equal(70, r2.AgeDays);
        Assert.Equal("Range6190", r2.Bucket);
        Assert.Equal(150m, r2.PaidAmount);

        var payable = report.Rows.First(r => r.VoucherNo == "PINV-001");
        Assert.Equal(-10, payable.AgeDays);
        Assert.Equal("Range0NotDue", payable.Bucket);

        Assert.Equal(150m, report.Totals.Receivable.Outstanding);
        Assert.Equal(100m, report.Totals.Receivable.Range030);
        Assert.Equal(50m, report.Totals.Receivable.Range6190);
        Assert.Equal(500m, report.Totals.Payable.Outstanding);
        Assert.Equal(500m, report.Totals.Payable.NotDue);
    }

    [Fact]
    public void AgingBucket_BoundariesMatchErpNextGrid()
    {
        Assert.Equal("Range0NotDue", GetAgingReportQueryHandler.BucketFor(-1));
        Assert.Equal("Range030", GetAgingReportQueryHandler.BucketFor(0));
        Assert.Equal("Range030", GetAgingReportQueryHandler.BucketFor(30));
        Assert.Equal("Range3160", GetAgingReportQueryHandler.BucketFor(31));
        Assert.Equal("Range3160", GetAgingReportQueryHandler.BucketFor(60));
        Assert.Equal("Range6190", GetAgingReportQueryHandler.BucketFor(61));
        Assert.Equal("Range6190", GetAgingReportQueryHandler.BucketFor(90));
        Assert.Equal("Range90Plus", GetAgingReportQueryHandler.BucketFor(91));
    }
}
