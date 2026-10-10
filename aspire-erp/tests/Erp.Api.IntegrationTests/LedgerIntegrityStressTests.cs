using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Erp.Application.Common;
using Erp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Api.IntegrationTests;

/// <summary>
/// Task 8.2 acceptance against the LIVE dev SQL container: the FULLOnce procurement-to-cash
/// cycle (PO → Receipt → Bill → SO → Delivery → Invoice → Payment) through the real HTTP
/// stack, then the two fundamental invariants over the vouchers of THIS run only:
/// (1) Σ(Debit − Credit) == 0.0000 (Constitution III.1), and (2) the GL stock legs equal the
/// Kardex value movement (GL ↔ StockLedger agreement).
/// </summary>
/// <remarks>
/// <para><b>LedgerMutating collection.</b> The cycle appends StockLedgerEntry AND GLEntry rows,
/// so this class serializes with the fiscal-lock/journal/report suites (same reason as the
/// sales and purchase lifecycle classes).</para>
///
/// <para><b>Run-scoped assertions.</b> Every sum is filtered to the voucher numbers collected
/// during THIS run - sibling suites post concurrently to the same company, so a global sum
/// would be flaky by construction. Leftover quantities are neutral (receipt +10, delivery −4:
/// the item ends 6 units above baseline, identical in Kardex and GL).</para>
///
/// <para><b>Cleanup.</b> Sells-side drafts and posted selling documents created here are removed
/// by id in FK-safe order (allocations → payment → invoice lines → invoice → delivery lines →
/// delivery → order lines → order → customer); the buy-side receipt/bill stay, exactly like the
/// purchase suites document ("documents are never cleaned up"). Ledger and Kardex rows remain
/// (Constitution Article III.2, append-only).</para>
/// </remarks>
[Collection(LedgerMutatingCollection.Name)]
public class LedgerIntegrityStressTests : IClassFixture<ErpApiFactory>, IDisposable
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    private static readonly Guid SupplierId = Guid.Parse("e0000000-0000-4000-8000-000000000001");
    private static readonly Guid WarehouseId = Guid.Parse("d0000000-0000-4000-8000-000000000002");
    private static readonly Guid BankAccountId = Guid.Parse("b0000000-0000-4000-8000-000000000001");

    /// <summary>scripts/seed-dev-stock.sql: Kilogram UOM backing every run-local item.</summary>
    private static readonly Guid UomId = Guid.Parse("b0000000-0000-4000-8000-000000000002");

    private const decimal ReceiptQty = 10m;
    private const decimal ReceiptRate = 100m;
    private const decimal SellQty = 4m;
    private const decimal SellRate = 150m;

    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    private readonly ErpApiFactory _factory;
    private readonly string _tag = $"T8B-{Guid.NewGuid():N}"[..12];

    private Guid _customerId;
    private Guid _itemId;
    private Guid _orderId;
    private Guid _deliveryId;
    private Guid _invoiceId;
    private Guid _paymentId;
    private readonly List<string> _voucherNos = new();

    public LedgerIntegrityStressTests(ErpApiFactory factory) => _factory = factory;

    /// <summary>
    /// The full cycle posts five GL-bearing vouchers (receipt, bill, delivery, invoice, payment);
    /// their combined Σ(Debit − Credit) must be EXACTLY zero - the fundamental invariant.
    /// </summary>
    [Fact]
    public async Task FullCycle_VoucherScopedLedger_SumsToExactlyZero()
    {
        await DriveFullCycleAsync();

        await using var context = CreateContext();
        var imbalance = await context.GLEntries
            .Where(e => e.CompanyId == ErpApiFactory.DevCompanyId && _voucherNos.Contains(e.VoucherNo))
            .SumAsync(e => e.Debit - e.Credit);

        Assert.Equal(0m, imbalance);
    }

    /// <summary>
    /// The stock legs the GL booked for this run's receipt + delivery must equal the Kardex
    /// value movement of the same two vouchers - no valuation leakage between subledgers.
    /// </summary>
    /// <remarks>
    /// Stock legs are identified by the WAREHOUSE's stock account resolved at runtime
    /// (WH-01's <c>AccountId</c>), never by the account <c>Type</c> label: long-lived dev
    /// databases can drift from the migration backfill (here 1310 reads <c>Other</c>), while
    /// the posting engine always resolves the warehouse account. Exact FIFO rates are NOT
    /// asserted - IT-001 is shared with concurrent suites, so the delivery's unit cost is
    /// deliberately non-deterministic; the invariant is GL == Kardex, whatever the rate.
    /// </remarks>
    [Fact]
    public async Task FullCycle_StockLegs_EqualsKardexValueMovement()
    {
        var vouchers = await DriveFullCycleAsync();

        await using var context = CreateContext();
        var stockAccountId = await context.Warehouses
            .Where(w => w.Id == WarehouseId)
            .Select(w => w.AccountId)
            .FirstAsync();

        var glStock = await context.GLEntries
            .Where(e => e.CompanyId == ErpApiFactory.DevCompanyId
                && (e.VoucherNo == vouchers.ReceiptVoucherNo || e.VoucherNo == vouchers.DeliveryVoucherNo)
                && e.AccountId == stockAccountId)
            .SumAsync(e => e.Debit - e.Credit);

        var kardexRows = await context.StockLedgerEntries
            .Where(e => e.VoucherNo == vouchers.ReceiptVoucherNo || e.VoucherNo == vouchers.DeliveryVoucherNo)
            .ToListAsync();
        var kardex = kardexRows.Sum(e => e.Amount);

        // The receipt leg is fully deterministic (MY rate): +10 × 100; the delivery leg is
        // negative (stock left the warehouse).
        Assert.Equal(ReceiptQty * ReceiptRate, kardexRows.Where(e => e.VoucherNo == vouchers.ReceiptVoucherNo).Sum(e => e.Amount));
        Assert.True(kardexRows.Where(e => e.VoucherNo == vouchers.DeliveryVoucherNo).Sum(e => e.Amount) < 0m);
        Assert.Equal(kardex, glStock);
    }

    /// <summary>
    /// The cycle ends with the customer invoice fully settled: Outstanding 0 and Paid status.
    /// </summary>
    [Fact]
    public async Task FullCycle_SalesInvoice_EndsFullyPaid()
    {
        await DriveFullCycleAsync();

        using var client = CreateClient();
        using var response = await client.GetAsync(
            $"/api/v1/sales-invoices/{_invoiceId}?companyId={ErpApiFactory.DevCompanyId}");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Invoice GET {(int)response.StatusCode}: {body}");

        var invoice = JsonNode.Parse(body)!;
        Assert.Equal(0m, invoice["outstandingAmount"]!.GetValue<decimal>());
        Assert.Equal("Paid", invoice["status"]!.GetValue<string>());
    }

    public void Dispose()
    {
        if (_paymentId == Guid.Empty)
        {
            return;
        }

        using var connection = new Microsoft.Data.SqlClient.SqlConnection(ErpApiFactory.DevConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            "DECLARE @pay AS TABLE (Id UNIQUEIDENTIFIER); "
            + "INSERT INTO @pay VALUES (@PaymentId); "
            + "DELETE FROM dbo.PaymentAllocation WHERE PaymentEntryId IN (SELECT Id FROM @pay); "
            + "DELETE FROM dbo.PaymentEntry WHERE Id IN (SELECT Id FROM @pay); "
            + "DELETE FROM dbo.SalesInvoiceItem WHERE SalesInvoiceId = @InvoiceId; "
            + "DELETE FROM dbo.SalesInvoice WHERE Id = @InvoiceId; "
            + "DELETE FROM dbo.DeliveryNoteLine WHERE DeliveryNoteId = @DeliveryId; "
            + "DELETE FROM dbo.DeliveryNote WHERE Id = @DeliveryId; "
            + "DELETE FROM dbo.SalesOrderItem WHERE SalesOrderId = @OrderId; "
            + "DELETE FROM dbo.SalesOrder WHERE Id = @OrderId; "
            + "DELETE FROM dbo.Customer WHERE Id = @CustomerId;";
        command.Parameters.AddWithValue("@PaymentId", _paymentId);
        command.Parameters.AddWithValue("@InvoiceId", _invoiceId);
        command.Parameters.AddWithValue("@DeliveryId", _deliveryId);
        command.Parameters.AddWithValue("@OrderId", _orderId);
        command.Parameters.AddWithValue("@CustomerId", _customerId);
        command.ExecuteNonQuery();
    }

    // ----------------------------------------------------------------------- the cycle

    private sealed record CycleVouchers(string ReceiptVoucherNo, string DeliveryVoucherNo);

    /// <summary>
    /// Drives PO → Receipt → Bill → SO → Delivery → Invoice → Payment over HTTP and records
    /// every id needed by the oracle assertions and the total cleanup.
    /// </summary>
    private async Task<CycleVouchers> DriveFullCycleAsync()
    {
        using var client = CreateClient();
        var today = Format(DateOnly.FromDateTime(DateTime.UtcNow));
        var companyId = ErpApiFactory.DevCompanyId;

        // -- master ---------------------------------------------------------------
        // Run-local item (unique code): its FIFO layers contain ONLY this run's receipt,
        // so the delivery unit cost is deterministic (receipt rate) instead of depending on
        // the shared IT-001 layers that concurrent suites consume (Phase 8 finding H3).
        // The item stays behind (receipt/bill lines reference it) like every posted master.
        _customerId = await CreateCustomerAsync(client);
        var itemRes = await PostJsonAsync(client, "/api/v1/items", new
        {
            code = $"T8B-IT-{Guid.NewGuid():N}"[..16],
            name = "T8B Stress Item",
            valuationMethod = "Fifo",
            baseUOMId = UomId,
        });
        _itemId = (await ReadCreatedAsync(itemRes, "Item POST"))["id"]!.GetValue<Guid>();

        // -- buy leg: PO (Draft → Submitted) ----------------------------------------
        var po = await PostJsonAsync(client, "/api/v1/purchaseorders", new
        {
            companyId,
            supplierId = SupplierId,
            transactionDate = today,
            scheduleDate = today,
            items = new[] { new { itemId = _itemId, quantity = ReceiptQty, rate = ReceiptRate } },
        });
        var poId = (await ReadCreatedAsync(po, "Order POST"))["id"]!.GetValue<Guid>();

        using var poSubmit = await client.PostAsync(
            $"/api/v1/purchaseorders/{poId}/submit?companyId={companyId}",
            new StringContent(string.Empty, Encoding.UTF8, "application/json"));
        Assert.True(poSubmit.StatusCode == HttpStatusCode.OK, $"PO submit {(int)poSubmit.StatusCode}");

        // -- buy leg: receipt (creates AND posts) ------------------------------------
        var receipt = await PostJsonAsync(client, "/api/v1/purchasereceipts", new
        {
            companyId,
            warehouseId = WarehouseId,
            supplierId = SupplierId,
            purchaseOrderId = poId,
            postingDate = today,
            lines = new[] { new { itemId = _itemId, qty = ReceiptQty, rate = ReceiptRate } },
        }, key: true);
        var receiptBody = (await ReadCreatedAsync(receipt, "Receipt POST"))["receipt"]!;
        var receiptVoucherNo = receiptBody["voucherNo"]!.GetValue<string>();
        var receiptLineId = receiptBody["lines"]![0]!["id"]!.GetValue<Guid>();
        _voucherNos.Add(receiptVoucherNo);

        // -- buy leg: bill (creates AND posts) ----------------------------------------
        var bill = await PostJsonAsync(client, "/api/v1/purchaseinvoices", new
        {
            companyId,
            supplierId = SupplierId,
            billNumber = $"{_tag}-BILL",
            postingDate = today,
            dueDate = today,
            taxAmount = 0m,
            lines = new[] { new { purchaseReceiptLineId = receiptLineId, itemId = _itemId, qty = ReceiptQty, rate = ReceiptRate } },
        }, key: true);
        var billBody = (await ReadCreatedAsync(bill, "Bill POST"))["invoice"]!;
        _voucherNos.Add(billBody["voucherNo"]!.GetValue<string>());

        // -- sell leg: order (Draft → Submitted through the credit gate) --------------
        var order = await PostJsonAsync(client, "/api/v1/sales-orders", new
        {
            companyId,
            customerId = _customerId,
            transactionDate = today,
            deliveryDate = today,
            lines = new[] { new { itemId = _itemId, quantity = SellQty, rate = SellRate } },
        });
        var orderBody = await ReadCreatedAsync(order, "Order POST");
        _orderId = orderBody["id"]!.GetValue<Guid>();
        var orderLineId = orderBody["lines"]![0]!["id"]!.GetValue<Guid>();

        using var soSubmit = await client.PostAsync(
            $"/api/v1/sales-orders/{_orderId}/submit?companyId={companyId}",
            new StringContent(string.Empty, Encoding.UTF8, "application/json"));
        Assert.True(soSubmit.StatusCode == HttpStatusCode.OK, $"SO submit {(int)soSubmit.StatusCode}");

        // -- sell leg: delivery (creates AND posts: FIFO relief + COGS) ----------------
        var delivery = await PostJsonAsync(client, "/api/v1/delivery-notes", new
        {
            companyId,
            salesOrderId = _orderId,
            warehouseId = WarehouseId,
            postingDate = today,
            lines = new[] { new { salesOrderItemId = orderLineId, itemId = _itemId, qty = SellQty } },
        }, key: true);
        var deliveryBody = await ReadCreatedAsync(delivery, "Delivery POST");
        _deliveryId = deliveryBody["deliveryNote"]!["id"]!.GetValue<Guid>();
        var deliveryVoucherNo = deliveryBody["deliveryNote"]!["voucherNo"]!.GetValue<string>();
        _voucherNos.Add(deliveryVoucherNo);

        // -- sell leg: invoice (Draft → Submitted: A/R + revenue) ----------------------
        var invoice = await PostJsonAsync(client, "/api/v1/sales-invoices", new
        {
            companyId,
            customerId = _customerId,
            postingDate = today,
            items = new[] { new { itemId = _itemId, quantity = SellQty, rate = SellRate } },
        });
        var invoiceBody = await ReadCreatedAsync(invoice, "Invoice POST");
        _invoiceId = invoiceBody["id"]!.GetValue<Guid>();
        _voucherNos.Add(invoiceBody["invoiceNumber"]!.GetValue<string>());

        using var sinvSubmit = await client.PostAsync(
            $"/api/v1/sales-invoices/{_invoiceId}/submit?companyId={companyId}",
            new StringContent(string.Empty, Encoding.UTF8, "application/json"));
        Assert.True(sinvSubmit.StatusCode == HttpStatusCode.OK, $"Invoice submit {(int)sinvSubmit.StatusCode}");

        // -- cash leg: payment in full (Draft → Submitted: bank + A/R settlement) ------
        var payment = await PostJsonAsync(client, "/api/v1/PaymentEntries", new
        {
            companyId,
            paymentType = "Receive",
            partyType = "Customer",
            partyId = _customerId,
            bankAccountId = BankAccountId,
            paymentDate = today,
            paidAmount = SellQty * SellRate,
            transactionCurrencyId = (Guid?)null,
            referenceNumber = $"{_tag}-REF",
            allocations = new[] { new { salesInvoiceId = _invoiceId, purchaseInvoiceId = (Guid?)null, allocatedAmount = SellQty * SellRate } },
        }, key: true);
        var paymentBody = await ReadCreatedAsync(payment, "Payment POST");
        _paymentId = paymentBody["id"]!.GetValue<Guid>();
        var rowVersion = paymentBody["rowVersion"]!.GetValue<string>();

        using var paySubmit = await PostJsonAsync(
            client, $"/api/v1/PaymentEntries/{_paymentId}/submit?companyId={companyId}",
            new { rowVersion }, key: true);
        var submitted = await ReadOkAsync(paySubmit, "Payment submit");
        _voucherNos.Add(submitted["voucherNo"]!.GetValue<string>());

        return new CycleVouchers(receiptVoucherNo, deliveryVoucherNo);
    }

    // ----------------------------------------------------------------------- helpers

    private HttpClient CreateClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-ID", ErpApiFactory.DevTenantId.ToString());
        return client;
    }

    private static AppDbContext CreateContext() =>
        new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(ErpApiFactory.DevConnectionString)
                .Options,
            new StubTenantProvider(ErpApiFactory.DevTenantId));

    private async Task<Guid> CreateCustomerAsync(HttpClient client)
    {
        using var response = await PostJsonAsync(client, "/api/v1/customers", new
        {
            companyId = ErpApiFactory.DevCompanyId,
            customerCode = $"{_tag}-CUST",
            customerName = "T8B Buyer",
        });

        return (await ReadCreatedAsync(response, "Customer POST"))["id"]!.GetValue<Guid>();
    }

    private static async Task<HttpResponseMessage> PostJsonAsync<T>(
        HttpClient client, string url, T payload, bool key = false)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload, options: PayloadOptions),
        };

        if (key)
        {
            request.Headers.Add(IdempotencyKeyHeader, Guid.NewGuid().ToString("N"));
        }

        return await client.SendAsync(request);
    }

    private static async Task<JsonObject> ReadCreatedAsync(HttpResponseMessage response, string label)
    {
        var body = await response.Content.ReadAsStringAsync();
        var status = response.StatusCode;
        response.Dispose();
        Assert.True(
            status == HttpStatusCode.Created,
            $"{label} {(int)status}: {body}");
        return (JsonObject)JsonNode.Parse(body)!;
    }

    private static async Task<JsonObject> ReadOkAsync(HttpResponseMessage response, string label)
    {
        var body = await response.Content.ReadAsStringAsync();
        var status = response.StatusCode;
        response.Dispose();
        Assert.True(
            status == HttpStatusCode.OK,
            $"{label} {(int)status}: {body}");
        return (JsonObject)JsonNode.Parse(body)!;
    }

    private static string Format(DateOnly date) =>
        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private sealed class StubTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;

        public StubTenantProvider(Guid tenantId) => _tenantId = tenantId;

        public Guid GetCurrentTenantId() => _tenantId;

        public bool HasTenant() => _tenantId != Guid.Empty;

        public void SetCurrentTenantId(Guid tenantId) => throw new NotSupportedException();
    }
}
