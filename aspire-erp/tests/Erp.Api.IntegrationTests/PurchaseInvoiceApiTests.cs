using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Erp.Api.IntegrationTests;

/// <summary>
/// Task 4.6 end-to-end evidence for the buying cancellation, against the LIVE dev container:
/// spec BY-04 (an idempotent REPLAY answers 200 with the stored body and books NOTHING new) and
/// spec BY-05 (cancel appends the compensating reversal, zeroes the bill and restores Accounts
/// Payable), plus the RFC 7807 conflicts the cancel endpoint must never swallow.
/// </summary>
/// <remarks>
/// <para><b>The oracle is the GENERAL-LEDGER REPORT, not only the response.</b> A 200 only proves
/// the API answered; BY-04 and BY-05 are statements about <c>GLEntry</c> ITSELF ("no additional
/// rows" / "one row per original line with the sides swapped"). Every test therefore reads
/// <c>GET /api/v1/FinancialReports/general-ledger</c> filtered by the invoice's
/// <c>voucherId</c> - a freshly generated GUID no other voucher can claim - so assertions stay
/// immune to ledger rows other tests append (and nobody can delete them: Constitution III.2).</para>
///
/// <para><b>Serialized with the other ledger-mutating classes through
/// <see cref="LedgerMutatingCollection"/>.</b> <see cref="FiscalPeriodLockApiTests"/> asserts a
/// GLOBAL ledger row count and flips <c>Company.FrozenAccountsDate</c>; running side by side with
/// a class that appends rows would make it flaky.</para>
///
/// <para><b>Documented side effect:</b> every successful chain LEAVES its rows behind (one
/// PurchaseReceipt, one PurchaseInvoice and their append-only stock/GL rows). That is intentional
/// - the ledger can never be cleaned up by hand - and the assertions are scoped to the fresh
/// voucher ids, so re-runs only ever ADD rows. Voucher numbers are therefore read back, never
/// hard-coded.</para>
///
/// <para><b>Seed data</b> comes from scripts/seed-dev-buying.sql and scripts/seed-dev-stock.sql:
/// supplier <c>SUP-001</c>, item <c>IT-001</c> (FIFO), warehouse <c>WH-01</c> posting to 1310, and
/// the company defaults 2110 / 1130 / 2120 with an open (unfrozen) fiscal period.</para>
/// </remarks>
[Collection(LedgerMutatingCollection.Name)]
public class PurchaseInvoiceApiTests : IClassFixture<ErpApiFactory>
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    /// <summary>Demo supplier SUP-001 (seed-dev-buying.sql).</summary>
    private static readonly Guid SupplierId =
        Guid.Parse("e0000000-0000-4000-8000-000000000001");

    /// <summary>Item IT-001 "Steel Bracket" (seed-dev-stock.sql).</summary>
    private static readonly Guid ItemId =
        Guid.Parse("c0000000-0000-4000-8000-000000000001");

    /// <summary>Warehouse WH-01 "Main Stores" - leaf, account 1310 (seed-dev-stock.sql).</summary>
    private static readonly Guid WarehouseId =
        Guid.Parse("d0000000-0000-4000-8000-000000000002");

    /// <summary>Account 2110 Accounts Payable - the balance this class must restore.</summary>
    private const string PayableAccountCode = "2110";

    private const decimal Qty = 10m;
    private const decimal Rate = 100m;
    private const decimal TaxAmount = 100m;

    /// <summary>The canonical BY-01 triple: Dr 2120 1000 + Dr 1130 100 / Cr 2110 1100.</summary>
    private static readonly decimal GrandTotal = Qty * Rate + TaxAmount;

    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    private readonly ErpApiFactory _factory;

    public PurchaseInvoiceApiTests(ErpApiFactory factory) => _factory = factory;

    // ------------------------------------------------------------------------------ BY-04

    /// <summary>
    /// Spec BY-04: re-sending the SAME request with the SAME <c>Idempotency-Key</c> answers
    /// <b>200 OK</b> (not the stored 201) with the original body byte-for-byte, and the ledger
    /// keeps exactly the three rows the first call wrote - a replay must never book twice. The
    /// very same payload WITHOUT the header is rejected with <c>idempotency_key_required</c>
    /// before any work happens.
    /// </summary>
    [Fact]
    public async Task Create_ReplayedWithSameKey_Returns200WithIdenticalBodyAndBooksNothingNew()
    {
        using var client = CreateClient();
        var key = Guid.NewGuid().ToString("N");

        var posted = await CreatePostedInvoiceAsync(client, BillNumber(), key);
        Assert.Equal(HttpStatusCode.Created, posted.Status);

        var before = await ReadVoucherLedgerAsync(client, posted.Id);
        Assert.Equal(3, before.Count);
        Assert.Equal(GrandTotal, before.Sum(row => row.Debit));
        Assert.Equal(GrandTotal, before.Sum(row => row.Credit));

        // The replay: identical URL, identical raw body, identical key.
        using var replay = await PostRawAsync(
            client, "/api/v1/purchaseinvoices", posted.RawRequestBody, key);

        // BY-04: the completed request is acknowledged with 200 OK, whatever it answered first,
        // carrying the stored body BYTE FOR BYTE (hex makes the comparison unambiguous).
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(
            Convert.ToHexString(posted.ResponseBody),
            Convert.ToHexString(await replay.Content.ReadAsByteArrayAsync()));

        var after = await ReadVoucherLedgerAsync(client, posted.Id);
        Assert.Equal(before.Count, after.Count);
        Assert.Equal(
            before.Select(row => row.Id).OrderBy(id => id),
            after.Select(row => row.Id).OrderBy(id => id));

        // No header -> the guard rejects before model binding, so not a single row is written.
        using var withoutKey = await PostRawAsync(
            client, "/api/v1/purchaseinvoices", posted.RawRequestBody, idempotencyKey: null);

        var problem = await AssertProblemAsync(withoutKey, HttpStatusCode.BadRequest);
        Assert.Equal("idempotency_key_required", problem["code"]!.GetValue<string>());
        Assert.Equal(before.Count, (await ReadVoucherLedgerAsync(client, posted.Id)).Count);
    }

    // ------------------------------------------------------------------------------ BY-05

    /// <summary>
    /// Spec BY-05 / tasks.md 4.6: cancelling a posted bill returns 200 with the header moved to
    /// <c>Cancelled</c> and its outstanding balance at 0, appends ONE mirror row per original line
    /// (Debit/Credit swapped, original posting date, <c>isCancelled = true</c>), leaves the
    /// originals untouched and nets Accounts Payable back to zero for that voucher.
    /// </summary>
    [Fact]
    public async Task Cancel_PostedInvoice_Returns200AppendsSwappedReversalAndRestoresPayable()
    {
        using var client = CreateClient();

        var posted = await CreatePostedInvoiceAsync(client, BillNumber());
        Assert.Equal("Unpaid", posted.Body["status"]!.GetValue<string>());

        var originals = await ReadVoucherLedgerAsync(client, posted.Id);
        Assert.Equal(3, originals.Count);
        Assert.All(originals, row => Assert.False(row.IsCancelled));
        Assert.Equal(GrandTotal, originals.Sum(row => row.Debit));
        Assert.Equal(GrandTotal, originals.Sum(row => row.Credit));

        using var cancelResponse = await PostRawAsync(
            client,
            $"/api/v1/purchaseinvoices/{posted.Id}/cancel?companyId={ErpApiFactory.DevCompanyId}",
            "{}",
            Guid.NewGuid().ToString("N"));

        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        var cancelled = JsonNode.Parse(await cancelResponse.Content.ReadAsStringAsync())!;
        Assert.Equal("Cancelled", cancelled["status"]!.GetValue<string>());
        Assert.Equal(0m, cancelled["outstandingAmount"]!.GetValue<decimal>());

        var rows = await ReadVoucherLedgerAsync(client, posted.Id);
        Assert.Equal(6, rows.Count); // 3 originals + 3 compensating reversals

        var reversalRows = rows.Where(row => row.IsCancelled).ToList();
        Assert.Equal(originals.Count, reversalRows.Count);

        foreach (var original in originals)
        {
            // Constitution III.2: the original row is still there, byte-for-byte.
            var persistedOriginal = Assert.Single(rows, row => row.Id == original.Id);
            Assert.False(persistedOriginal.IsCancelled);
            Assert.Equal(original.Debit, persistedOriginal.Debit);
            Assert.Equal(original.Credit, persistedOriginal.Credit);

            // The mirror image: same account, sides swapped, SAME voucher and posting date.
            var reversal = Assert.Single(reversalRows, row => row.AccountId == original.AccountId);
            Assert.Equal(original.Credit, reversal.Debit);
            Assert.Equal(original.Debit, reversal.Credit);
            Assert.Equal(original.PostingDate, reversal.PostingDate);
            Assert.Equal(posted.Id, reversal.VoucherId);
            Assert.Equal(posted.VoucherNo, reversal.VoucherNo);
            Assert.Equal("PurchaseInvoice", reversal.VoucherType);
            Assert.Contains(posted.VoucherNo, reversal.Remarks);
        }

        // Trial-balance neutrality of the voucher: debits == credits after the reversal.
        Assert.Equal(rows.Sum(row => row.Debit), rows.Sum(row => row.Credit));

        // Accounts Payable is restored: the payable row and its mirror cancel each other out.
        Assert.Equal(
            0m,
            rows.Where(row => row.AccountCode == PayableAccountCode)
                .Sum(row => row.Debit - row.Credit));
    }

    /// <summary>
    /// A second cancellation is a STATE conflict, not a new reversal: 409
    /// <c>invoice_already_cancelled</c> and the ledger stays at six rows.
    /// </summary>
    [Fact]
    public async Task Cancel_Twice_Returns409AlreadyCancelledAndKeepsLedgerAtSixRows()
    {
        using var client = CreateClient();
        var posted = await CreatePostedInvoiceAsync(client, BillNumber());

        using var first = await PostRawAsync(
            client,
            $"/api/v1/purchaseinvoices/{posted.Id}/cancel?companyId={ErpApiFactory.DevCompanyId}",
            "{}",
            Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var second = await PostRawAsync(
            client,
            $"/api/v1/purchaseinvoices/{posted.Id}/cancel?companyId={ErpApiFactory.DevCompanyId}",
            "{}",
            Guid.NewGuid().ToString("N"));

        var problem = await AssertProblemAsync(second, HttpStatusCode.Conflict);
        Assert.Equal("invoice_already_cancelled", problem["code"]!.GetValue<string>());

        Assert.Equal(6, (await ReadVoucherLedgerAsync(client, posted.Id)).Count);
    }

    /// <summary>
    /// The endpoint is a guarded mutation and hides unknown ids: no <c>Idempotency-Key</c> is 400,
    /// an id that does not exist in this tenant is 404 <c>purchase_invoice_not_found</c>.
    /// </summary>
    [Fact]
    public async Task Cancel_WithoutKeyIs400_And_UnknownInvoiceIs404()
    {
        using var client = CreateClient();

        using var unknown = await PostRawAsync(
            client,
            $"/api/v1/purchaseinvoices/{Guid.NewGuid()}/cancel?companyId={ErpApiFactory.DevCompanyId}",
            "{}",
            Guid.NewGuid().ToString("N"));

        var notFound = await AssertProblemAsync(unknown, HttpStatusCode.NotFound);
        Assert.Equal("purchase_invoice_not_found", notFound["code"]!.GetValue<string>());

        var posted = await CreatePostedInvoiceAsync(client, BillNumber());

        using var withoutKey = await PostRawAsync(
            client,
            $"/api/v1/purchaseinvoices/{posted.Id}/cancel?companyId={ErpApiFactory.DevCompanyId}",
            "{}",
            idempotencyKey: null);

        var missingKey = await AssertProblemAsync(withoutKey, HttpStatusCode.BadRequest);
        Assert.Equal("idempotency_key_required", missingKey["code"]!.GetValue<string>());

        // Rejected BEFORE any work: the bill is still Unpaid with its three ledger rows.
        Assert.Equal(3, (await ReadVoucherLedgerAsync(client, posted.Id)).Count);
    }

    // ------------------------------------------------------------------------------- helpers

    /// <summary>Tenant-scoped client (X-Tenant-ID only - see ErpApiFactory remarks on auth).</summary>
    private HttpClient CreateClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-ID", ErpApiFactory.DevTenantId.ToString());
        return client;
    }

    /// <summary>A bill number that no previous run can have used (documents are never cleaned up).</summary>
    private static string BillNumber() => $"B{Guid.NewGuid():N}";

    /// <summary>
    /// Drives the posting chain the cancellation needs - receipt (201 + key, NO purchase order)
    /// -&gt; invoice (201 + key) - and returns the invoice payload together with the EXACT raw
    /// request body, which is what the idempotency filter hashes.
    /// </summary>
    /// <remarks>
    /// The order leg is deliberately omitted: <c>PurchaseOrderId</c> is optional by contract and
    /// neither spec BY-04 nor BY-05 says anything about the order workflow, so the evidence does
    /// not depend on <c>POST /api/v1/purchaseorders</c>. (The unrelated voucher-column defect
    /// that endpoint had - a raw <c>MAX(VoucherNo)</c> against a table whose column is
    /// <c>OrderNumber</c> - was fixed separately and is carried by
    /// <see cref="PurchaseOrdersApiTests"/>.)
    /// </remarks>
    private async Task<PostedInvoice> CreatePostedInvoiceAsync(
        HttpClient client,
        string billNumber,
        string? invoiceIdempotencyKey = null)
    {
        var postingDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // The invoice POST is a guarded mutation: every chain needs its own key unless the test
        // supplies one (the replay scenario reuses the FIRST key on purpose).
        invoiceIdempotencyKey ??= Guid.NewGuid().ToString("N");

        var receiptPayload = SerializePayload(new
        {
            companyId = ErpApiFactory.DevCompanyId,
            warehouseId = WarehouseId,
            supplierId = SupplierId,
            postingDate = Format(postingDate),
            lines = new[] { new { itemId = ItemId, qty = Qty, rate = Rate } },
        });

        using var receiptResponse = await PostRawAsync(
            client,
            "/api/v1/purchasereceipts",
            receiptPayload,
            Guid.NewGuid().ToString("N"));
        var receiptBytes = await receiptResponse.Content.ReadAsByteArrayAsync();
        Assert.True(
            receiptResponse.StatusCode == HttpStatusCode.Created,
            $"Receipt POST {(int)receiptResponse.StatusCode}: {Encoding.UTF8.GetString(receiptBytes)}");
        var receiptPosting = JsonNode.Parse(Encoding.UTF8.GetString(receiptBytes))!;
        var receiptLineId = receiptPosting["receipt"]!["lines"]![0]!["id"]!.GetValue<Guid>();

        var invoicePayload = SerializePayload(new
        {
            companyId = ErpApiFactory.DevCompanyId,
            supplierId = SupplierId,
            billNumber,
            postingDate = Format(postingDate),
            dueDate = Format(postingDate.AddDays(30)),
            taxAmount = TaxAmount,
            lines = new[]
            {
                new
                {
                    purchaseReceiptLineId = receiptLineId,
                    itemId = ItemId,
                    qty = Qty,
                    rate = Rate,
                },
            },
        });

        using var invoiceResponse = await PostRawAsync(
            client, "/api/v1/purchaseinvoices", invoicePayload, invoiceIdempotencyKey);

        var responseBytes = await invoiceResponse.Content.ReadAsByteArrayAsync();

        Assert.True(
            invoiceResponse.StatusCode == HttpStatusCode.Created,
            $"Invoice POST {(int)invoiceResponse.StatusCode}: {Encoding.UTF8.GetString(responseBytes)}");

        // The endpoint answers with the FULL posting envelope (invoice + its ledger lines).
        var invoiceBody = JsonNode.Parse(Encoding.UTF8.GetString(responseBytes))!["invoice"]!;

        return new PostedInvoice(
            invoiceBody["id"]!.GetValue<Guid>(),
            invoiceBody["voucherNo"]!.GetValue<string>(),
            invoiceBody,
            invoicePayload,
            responseBytes,
            invoiceResponse.StatusCode);
    }

    /// <summary>
    /// POSTs one raw body (the idempotency filter hashes the RAW bytes, so a test that replays a
    /// request must reuse the very same string) with an optional idempotency key.
    /// </summary>
    private static Task<HttpResponseMessage> PostRawAsync(
        HttpClient client,
        string url,
        string rawBody,
        string? idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(rawBody, Encoding.UTF8, "application/json"),
        };

        if (idempotencyKey is not null)
        {
            request.Headers.Add(IdempotencyKeyHeader, idempotencyKey);
        }

        return client.SendAsync(request);
    }

    /// <summary>Serializes with the web defaults MVC uses, so the raw body matches what the API binds.</summary>
    private static string SerializePayload<T>(T payload) =>
        JsonSerializer.Serialize(payload, PayloadOptions);

    private static string Format(DateOnly date) =>
        date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Reads every ledger row of one invoice voucher from the pinned general-ledger contract -
    /// the BY-04/BY-05 oracle ("no additional rows", "one row per original line, sides swapped").
    /// </summary>
    private async Task<List<LedgerRow>> ReadVoucherLedgerAsync(HttpClient client, Guid voucherId)
    {
        using var response = await client.GetAsync(
            $"/api/v1/FinancialReports/general-ledger"
            + $"?companyId={ErpApiFactory.DevCompanyId}"
            + $"&voucherId={voucherId}&voucherType=PurchaseInvoice");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var report = (JsonObject)JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var rows = new List<LedgerRow>();

        foreach (var item in report["items"]!.AsArray())
        {
            rows.Add(new LedgerRow(
                item!["id"]!.GetValue<long>(),
                item["accountId"]!.GetValue<Guid>(),
                item["accountCode"]!.GetValue<string>(),
                item["debit"]!.GetValue<decimal>(),
                item["credit"]!.GetValue<decimal>(),
                item["isCancelled"]!.GetValue<bool>(),
                item["voucherType"]!.GetValue<string>(),
                item["voucherNo"]!.GetValue<string>(),
                item["voucherId"]!.GetValue<Guid>(),
                DateOnly.Parse(
                    item["postingDate"]!.GetValue<string>(),
                    System.Globalization.CultureInfo.InvariantCulture),
                item["remarks"]?.GetValue<string>()));
        }

        return rows;
    }

    /// <summary>Asserts the RFC 7807 contract of a rejection and returns the parsed problem.</summary>
    private static async Task<JsonNode> AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus)
    {
        Assert.Equal(expectedStatus, response.StatusCode);

        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal((int)expectedStatus, problem["status"]!.GetValue<int>());
        Assert.NotNull(problem["title"]!.GetValue<string>());
        return problem;
    }

    /// <summary>The posted bill: identity, DTO and the raw request/response of the guarded POST.</summary>
    private sealed record PostedInvoice(
        Guid Id,
        string VoucherNo,
        JsonNode Body,
        string RawRequestBody,
        byte[] ResponseBody,
        HttpStatusCode Status);

    /// <summary>Projection of one general-ledger report entry.</summary>
    private sealed record LedgerRow(
        long Id,
        Guid AccountId,
        string AccountCode,
        decimal Debit,
        decimal Credit,
        bool IsCancelled,
        string VoucherType,
        string VoucherNo,
        Guid VoucherId,
        DateOnly PostingDate,
        string? Remarks);
}
