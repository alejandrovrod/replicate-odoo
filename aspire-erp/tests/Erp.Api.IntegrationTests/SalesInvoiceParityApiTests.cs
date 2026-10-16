using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Erp.Api.IntegrationTests;

/// <summary>
/// Sales-invoice ERPNext parity end to end over HTTP (.specify/modules/
/// sales-invoice-erpnext-parity, scenarios SI-G1/SI-G2/SI-G3): the UpdateStock submit
/// posts revenue at the billed rate AND relieves FIFO layers with a Dr COGS / Cr Stock
/// pair plus its Kardex row, while a crossed advance nets the outstanding balance without
/// any compensation entry (the receipt already credited Receivable in full).
/// </summary>
/// <remarks>
/// <para><b>The oracle is the LEDGER, not only the response</b> (the PurchaseInvoiceApiTests
/// precedent): every statement below reads <c>GLEntry</c> itself - through the general-ledger
/// report scoped to the fresh voucher id - and the Kardex straight from the database, so a
/// 200 that booked the wrong money cannot pass.</para>
///
/// <para><b>Serialized with the other ledger-mutating classes through
/// <see cref="LedgerMutatingCollection"/>.</b> The submit appends GLEntry and StockLedgerEntry
/// rows, so this class must never overlap FiscalPeriodLockApiTests's global ledger count.</para>
///
/// <para><b>Stock provisioning is neutral.</b> Each stock test receipts EXACTLY what it later
/// bills, so on-hand lands back on its baseline when the test ends (receipt +q, invoice -q).
/// The ledger rows stay: Constitution Article III.2 makes the ledger append-only, so only
/// the SELLING documents are cleaned up below.</para>
///
/// <para><b>Customer prefix <c>SIIT-</c>.</b> CustomersApiTests deletes every <c>IT-%</c>
/// customer concurrently and SalesOrderDeliveryNoteApiTests owns <c>SOIT-%</c>: a third
/// prefix keeps all three teardowns disjoint.</para>
/// </remarks>
[Collection(LedgerMutatingCollection.Name)]
public class SalesInvoiceParityApiTests : IClassFixture<ErpApiFactory>, IDisposable
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";
    private const string CustomersPath = "/api/v1/customers";
    private const string SalesInvoicesPath = "/api/v1/sales-invoices";
    private const string PaymentEntriesPath = "/api/v1/payment-entries";
    private const string StockEntriesPath = "/api/v1/stockentries";

    /// <summary>scripts/seed-dev-stock.sql: IT-001 Steel Bracket (FIFO).</summary>
    private static readonly Guid ItemId = Guid.Parse("c0000000-0000-4000-8000-000000000001");

    /// <summary>scripts/seed-dev-stock.sql: WH-01 Main Stores, leaf posting to 1310.</summary>
    private static readonly Guid WarehouseId = Guid.Parse("d0000000-0000-4000-8000-000000000002");

    /// <summary>scripts/seed-dev-banking.sql: Main Operating, wired to 1110.</summary>
    private static readonly Guid BankAccountId = Guid.Parse("b0000000-0000-4000-8000-000000000001");

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    private readonly ErpApiFactory _factory;

    public SalesInvoiceParityApiTests(ErpApiFactory factory) => _factory = factory;

    // ------------------------------------------------------------------ SI-G1: UpdateStock

    /// <summary>
    /// Spec SI-G1: billing 2 units @ $100 with UpdateStock against the FIFO pool posts
    /// revenue at the BILLED rate (Dr 1120 $200 / Cr 4110 $200) and relieves stock at the
    /// FIFO layer cost (Dr 5210 / Cr 1310, paired and balanced) with one -2 Kardex row
    /// amount-locked to the COGS leg - all four legs plus the Kardex row born in one submit.
    /// </summary>
    [Fact]
    public async Task Submit_UpdateStockInvoice_PostsRevenueAtBilledRateAndRelievesFifoCost()
    {
        using var client = CreateClient();
        var customerId = await CreateCustomerAsync(client);

        await ReceiveStockAsync(client, quantity: 10m, rate: 50m);

        var invoice = await CreateInvoiceAsync(client, customerId, quantity: 2m, rate: 100m, updateStock: true);
        var invoiceId = invoice["id"]!.GetValue<Guid>();
        var invoiceNo = invoice["invoiceNumber"]!.GetValue<string>();

        // Kardex oracle scope: SINV numbers regenerate from the row count and Dispose deletes
        // this run's documents, so a LATER run can reuse this voucher number - the CreatedAt
        // cutoff below keeps previous runs' rows out of this run's assertion.
        var since = DateTimeOffset.UtcNow;

        using var submit = await client.PostAsync(
            $"{SalesInvoicesPath}/{invoiceId}/submit?companyId={ErpApiFactory.DevCompanyId}", null);

        var submitBody = await submit.Content.ReadAsStringAsync();
        Assert.True(submit.StatusCode == HttpStatusCode.OK, $"Submit {(int)submit.StatusCode}: {submitBody}");
        var submitted = JsonNode.Parse(submitBody)!;
        Assert.Equal("Unpaid", submitted["status"]!.GetValue<string>());
        Assert.Equal(200m, submitted["outstandingAmount"]!.GetValue<decimal>());

        // Money oracle: revenue at the billed rate (Dr 1120 $200 / Cr 4110 $200) plus
        // the paired FIFO relief (Dr 5210 / Cr 1310 at the layer cost). The exact layer
        // cost is intentionally NOT pinned here - the dev database shares one FIFO pool
        // across runs (see SalesOrderDeliveryNoteApiTests remarks), so the consumed layer
        // depends on run history; the deterministic FIFO valuation lives in the unit
        // suite. What this proves is simultaneity: A/R+revenue AND COGS+stock AND the
        // Kardex row are born in the same submit, paired and balanced.
        var rows = await ReadVoucherLedgerAsync(client, invoiceId, "Sales Invoice");
        Assert.Equal(4, rows.Count);
        Assert.Equal(rows.Sum(row => row.Debit), rows.Sum(row => row.Credit));
        AssertGl(rows, "1120", debit: 200m, credit: 0m);
        AssertGl(rows, "4110", debit: 0m, credit: 200m);

        var cogs = Assert.Single(rows, row => row.AccountCode == "5210");
        var stock = Assert.Single(rows, row => row.AccountCode == "1310");
        Assert.True(cogs.Debit > 0m && cogs.Credit == 0m);
        Assert.True(stock.Credit > 0m && stock.Debit == 0m);
        Assert.Equal(cogs.Debit, stock.Credit);

        // Kardex oracle: one issue row for the billed line, amount-locked to the COGS leg.
        var kardex = await ReadKardexAsync(invoiceNo, since);
        var issue = Assert.Single(kardex);
        Assert.Equal(-2m, issue.QtyChange);
        Assert.True(issue.ValuationRate > 0m);
        Assert.Equal(-cogs.Debit, issue.Amount);
    }

    // ------------------------------------------------------------------ SI-G2: advance cross

    /// <summary>
    /// Spec SI-G2: a $150 submitted receipt crossed against a $200 bill leaves $50
    /// outstanding (PartiallyPaid) and drains the payment's unallocated remainder to zero -
    /// with NO compensation entry: the receipt already credited 1120 in full, so the cross
    /// nets inside Receivable by construction.
    /// </summary>
    [Fact]
    public async Task Submit_InvoiceWithAdvanceCross_NetsOutstandingAndDrainsRemainder()
    {
        using var client = CreateClient();
        var customerId = await CreateCustomerAsync(client);

        var paymentId = await CreateAndSubmitAdvanceAsync(client, customerId, paidAmount: 150m);

        var eligible = await ReadEligibleAdvancesAsync(client, customerId);
        var funding = Assert.Single(eligible);
        Assert.Equal(paymentId, funding["paymentEntryId"]!.GetValue<Guid>());
        Assert.Equal(150m, funding["unallocatedAmount"]!.GetValue<decimal>());

        var invoice = await CreateInvoiceAsync(
            client, customerId, quantity: 2m, rate: 100m,
            advances: new[] { (paymentId, 150m) });
        var invoiceId = invoice["id"]!.GetValue<Guid>();
        Assert.Equal(150m, invoice["totalAdvance"]!.GetValue<decimal>());
        Assert.Equal(50m, invoice["outstandingAmount"]!.GetValue<decimal>());

        using var submit = await client.PostAsync(
            $"{SalesInvoicesPath}/{invoiceId}/submit?companyId={ErpApiFactory.DevCompanyId}", null);

        var submitBody = await submit.Content.ReadAsStringAsync();
        Assert.True(submit.StatusCode == HttpStatusCode.OK, $"Submit {(int)submit.StatusCode}: {submitBody}");
        var submitted = JsonNode.Parse(submitBody)!;
        Assert.Equal("PartiallyPaid", submitted["status"]!.GetValue<string>());
        Assert.Equal(50m, submitted["outstandingAmount"]!.GetValue<decimal>());
        Assert.Equal(150m, submitted["paidAmount"]!.GetValue<decimal>());
        Assert.Equal(150m, submitted["totalAdvance"]!.GetValue<decimal>());

        // The remainder is consumed: a second cross finds nothing to fund.
        Assert.Empty(await ReadEligibleAdvancesAsync(client, customerId));

        // Exactly the two base legs (A/R + revenue): the cross books no extra money.
        var rows = await ReadVoucherLedgerAsync(client, invoiceId, "Sales Invoice");
        Assert.Equal(2, rows.Count);
        AssertGl(rows, "1120", debit: 200m, credit: 0m);
        AssertGl(rows, "4110", debit: 0m, credit: 200m);
    }

    // ------------------------------------------------------------------ SI-G3: over-cross guard

    /// <summary>
    /// Spec SI-G3: reserving $500 of advance against a $200 bill is rejected with 422
    /// <c>advance_exceeds_invoice_total</c> - at creation, so the unbillable Draft never
    /// exists and no ledger row can precede the rejection.
    /// </summary>
    [Fact]
    public async Task Create_InvoiceWithAdvanceBeyondTotal_Returns422AdvanceExceeds()
    {
        using var client = CreateClient();
        var customerId = await CreateCustomerAsync(client);

        var paymentId = await CreateAndSubmitAdvanceAsync(client, customerId, paidAmount: 500m);

        var payload = SerializePayload(new
        {
            companyId = ErpApiFactory.DevCompanyId,
            customerId,
            postingDate = Iso(Today),
            items = new[] { new { itemId = ItemId, quantity = 2m, rate = 100m } },
            advances = new[] { new { paymentEntryId = paymentId, allocatedAmount = 500m } },
        });

        using var response = await client.PostAsync(
            SalesInvoicesPath, new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));

        var problem = await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity);
        Assert.Equal("advance_exceeds_invoice_total", problem["code"]!.GetValue<string>());
    }

    // ------------------------------------------------------------------------------- helpers

    /// <summary>Tenant-scoped client (X-Tenant-ID only - see ErpApiFactory remarks on auth).</summary>
    private HttpClient CreateClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-ID", ErpApiFactory.DevTenantId.ToString());
        return client;
    }

    /// <summary>Creates one <c>SIIT-</c> customer (credit control off) and returns its id.</summary>
    private async Task<Guid> CreateCustomerAsync(HttpClient client)
    {
        var payload = new
        {
            companyId = ErpApiFactory.DevCompanyId,
            customerCode = $"SIIT-{Guid.NewGuid():N}"[..13],
            customerName = "Parity Invoice Buyer",
            creditLimit = 0m,
            bypassCreditLimitCheck = false,
        };

        using var response = await client.PostAsJsonAsync(CustomersPath, payload);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Customer POST {(int)response.StatusCode}: {body}");
        return JsonNode.Parse(body)!["id"]!.GetValue<Guid>();
    }

    /// <summary>
    /// Posts a MaterialReceipt of IT-001 into WH-01 at the given rate: the FIFO layer the
    /// UpdateStock test consumes. Fresh Idempotency-Key per call (Constitution VI.4).
    /// </summary>
    private async Task ReceiveStockAsync(HttpClient client, decimal quantity, decimal rate)
    {
        var payload = SerializePayload(new
        {
            companyId = ErpApiFactory.DevCompanyId,
            entryType = "MaterialReceipt",
            warehouseId = WarehouseId,
            postingDate = Iso(Today),
            lines = new[] { new { itemId = ItemId, qty = quantity, rate } },
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, StockEntriesPath)
        {
            Content = JsonContent.Create(JsonNode.Parse(payload)),
        };
        request.Headers.Add(IdempotencyKeyHeader, Guid.NewGuid().ToString("N"));

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    /// <summary>
    /// Creates one single-line sales invoice (Draft, no GL impact) and returns its payload.
    /// </summary>
    private async Task<JsonNode> CreateInvoiceAsync(
        HttpClient client,
        Guid customerId,
        decimal quantity,
        decimal rate,
        bool updateStock = false,
        IReadOnlyList<(Guid PaymentId, decimal Amount)>? advances = null)
    {
        var payload = SerializePayload(new
        {
            companyId = ErpApiFactory.DevCompanyId,
            customerId,
            postingDate = Iso(Today),
            items = new[] { new { itemId = ItemId, quantity, rate } },
            updateStock,
            sourceWarehouseId = updateStock ? WarehouseId : (Guid?)null,
            advances = (advances ?? Array.Empty<(Guid, decimal)>())
                .Select(a => new { paymentEntryId = a.PaymentId, allocatedAmount = a.Amount })
                .ToArray(),
        });

        using var response = await client.PostAsync(
            SalesInvoicesPath, new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Invoice POST {(int)response.StatusCode}: {body}");
        return JsonNode.Parse(body)!;
    }

    /// <summary>
    /// Funds the cross: a Draft Receive voucher with no slices (pure advance) submitted with a
    /// fresh idempotency key. Returns the payment id.
    /// </summary>
    private async Task<Guid> CreateAndSubmitAdvanceAsync(HttpClient client, Guid customerId, decimal paidAmount)
    {
        var draft = SerializePayload(new
        {
            companyId = ErpApiFactory.DevCompanyId,
            paymentType = "Receive",
            partyType = "Customer",
            partyId = customerId,
            bankAccountId = BankAccountId,
            paymentDate = Iso(Today),
            paidAmount,
            allocations = Array.Empty<object>(),
        });

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, PaymentEntriesPath)
        {
            Content = new StringContent(draft, System.Text.Encoding.UTF8, "application/json"),
        };
        createRequest.Headers.Add(IdempotencyKeyHeader, Guid.NewGuid().ToString("N"));

        using var created = await client.SendAsync(createRequest);

        var createdBody = await created.Content.ReadAsStringAsync();
        Assert.True(created.StatusCode == HttpStatusCode.Created, $"Payment POST {(int)created.StatusCode}: {createdBody}");
        var payment = JsonNode.Parse(createdBody)!;
        var paymentId = payment["id"]!.GetValue<Guid>();
        var rowVersion = payment["rowVersion"]!.GetValue<string>();

        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{PaymentEntriesPath}/{paymentId}/submit?companyId={ErpApiFactory.DevCompanyId}")
        {
            Content = JsonContent.Create(new { rowVersion }),
        };
        request.Headers.Add(IdempotencyKeyHeader, Guid.NewGuid().ToString("N"));

        using var submitted = await client.SendAsync(request);
        var submittedBody = await submitted.Content.ReadAsStringAsync();
        Assert.True(submitted.StatusCode == HttpStatusCode.OK, $"Payment submit {(int)submitted.StatusCode}: {submittedBody}");

        return paymentId;
    }

    private async Task<JsonArray> ReadEligibleAdvancesAsync(HttpClient client, Guid customerId)
    {
        using var response = await client.GetAsync(
            $"/api/v1/customers/{customerId}/eligible-advances?companyId={ErpApiFactory.DevCompanyId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsArray();
    }

    /// <summary>
    /// Reads every ledger row of one voucher from the general-ledger report (the SI-G1/SI-G2
    /// oracle: response codes never prove money moved).
    /// </summary>
    private async Task<List<LedgerRow>> ReadVoucherLedgerAsync(
        HttpClient client, Guid voucherId, string voucherType)
    {
        using var response = await client.GetAsync(
            $"/api/v1/FinancialReports/general-ledger"
            + $"?companyId={ErpApiFactory.DevCompanyId}&pageSize=500"
            + $"&voucherId={voucherId}&voucherType={voucherType}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var report = (JsonObject)JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var rows = new List<LedgerRow>();

        foreach (var item in report["items"]!.AsArray())
        {
            rows.Add(new LedgerRow(
                item!["accountCode"]!.GetValue<string>(),
                item["debit"]!.GetValue<decimal>(),
                item["credit"]!.GetValue<decimal>()));
        }

        return rows;
    }

    private static void AssertGl(List<LedgerRow> rows, string accountCode, decimal debit, decimal credit)
    {
        var leg = Assert.Single(rows, row => row.AccountCode == accountCode);
        Assert.Equal(debit, leg.Debit);
        Assert.Equal(credit, leg.Credit);
    }

    /// <summary>Reads the Kardex rows of one voucher written after <paramref name="since"/>.</summary>
    private static async Task<List<KardexRow>> ReadKardexAsync(string voucherNo, DateTimeOffset since)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT QtyChange, ValuationRate, Amount "
            + "FROM dbo.StockLedgerEntry WITH (NOLOCK) "
            + "WHERE VoucherNo = @VoucherNo AND CreatedAt >= @Since;",
            connection);
        command.Parameters.AddWithValue("@VoucherNo", voucherNo);
        command.Parameters.AddWithValue("@Since", since);

        var rows = new List<KardexRow>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new KardexRow(reader.GetDecimal(0), reader.GetDecimal(1), reader.GetDecimal(2)));
        }

        return rows;
    }

    /// <summary>Asserts the RFC 7807 contract of a rejection and returns the parsed problem.</summary>
    private static async Task<JsonNode> AssertProblemAsync(
        HttpResponseMessage response, HttpStatusCode expectedStatus)
    {
        Assert.Equal(expectedStatus, response.StatusCode);

        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal((int)expectedStatus, problem["status"]!.GetValue<int>());
        Assert.NotNull(problem["title"]!.GetValue<string>());
        return problem;
    }

    private static string SerializePayload<T>(T payload) =>
        JsonSerializer.Serialize(payload, PayloadOptions);

    private static string Iso(DateOnly date) =>
        date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private sealed record LedgerRow(string AccountCode, decimal Debit, decimal Credit);

    private sealed record KardexRow(decimal QtyChange, decimal ValuationRate, decimal Amount);

    /// <summary>
    /// Removes the selling documents this class created (and the <c>SIIT-</c> customers they
    /// reference) so re-runs stay idempotent. FK order matters: allocation slices, then lines,
    /// then headers, then payments, then customers. Ledger/Kardex rows remain (append-only).
    /// </summary>
    public void Dispose()
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            "DECLARE @mine TABLE (Id UNIQUEIDENTIFIER); "
            + "INSERT INTO @mine SELECT Id FROM dbo.Customer "
            + "WHERE TenantId = @TenantId AND CustomerCode LIKE N'SIIT-%'; "
            + "DELETE FROM dbo.PaymentAllocation "
            + "WHERE SalesInvoiceId IN (SELECT Id FROM dbo.SalesInvoice WHERE CustomerId IN (SELECT Id FROM @mine)); "
            + "DELETE FROM dbo.PaymentAllocation "
            + "WHERE PaymentEntryId IN (SELECT Id FROM dbo.PaymentEntry WHERE PartyId IN (SELECT Id FROM @mine)); "
            + "DELETE FROM dbo.SalesInvoiceTax "
            + "WHERE SalesInvoiceId IN (SELECT Id FROM dbo.SalesInvoice WHERE CustomerId IN (SELECT Id FROM @mine)); "
            + "DELETE FROM dbo.SalesInvoiceItem "
            + "WHERE SalesInvoiceId IN (SELECT Id FROM dbo.SalesInvoice WHERE CustomerId IN (SELECT Id FROM @mine)); "
            + "DELETE FROM dbo.SalesInvoice WHERE CustomerId IN (SELECT Id FROM @mine); "
            + "DELETE FROM dbo.PaymentEntry WHERE PartyId IN (SELECT Id FROM @mine); "
            + "DELETE FROM dbo.Customer WHERE Id IN (SELECT Id FROM @mine);";
        command.Parameters.AddWithValue("@TenantId", ErpApiFactory.DevTenantId);
        command.ExecuteNonQuery();
    }
}
