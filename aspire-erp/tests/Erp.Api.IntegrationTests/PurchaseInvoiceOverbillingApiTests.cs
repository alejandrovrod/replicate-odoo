using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Erp.Api.IntegrationTests;

/// <summary>
/// Tasks 4.4 and 4.7 end-to-end evidence for the three-way match, against the LIVE dev container:
/// spec BY-03 (billing above the received quantity answers 400 <c>overbilling_not_allowed</c> with
/// the scenario's VERBATIM message and books NO ledger row at all) and spec BY-06 (two clerks
/// billing the SAME receipt line concurrently serialize on the row lock: exactly one 201, the loser
/// gets the BY-06 literal, and the receipt still accepts the remaining quantity afterwards - i.e.
/// progressive billing works, there is no unique-index backstop anymore).
/// </summary>
/// <remarks>
/// <para><b>The oracle is the GENERAL-LEDGER REPORT, not only the response.</b> BY-03 says "no
/// ledger entries are created", which a 400 alone cannot prove: every test therefore snapshots the
/// <c>totalDebit</c>/<c>totalCredit</c> pair of <c>GET /api/v1/FinancialReports/general-ledger</c>
/// (computed SERVER-SIDE over the FULL filtered set, never just the page) around the rejected POST
/// and asserts equality.</para>
///
/// <para><b>Serialized with the other ledger-mutating classes through
/// <see cref="LedgerMutatingCollection"/>.</b> The totals snapshot is only a sound "nothing was
/// written" oracle while no OTHER class can append rows in the same window, so this class joins the
/// collection instead of keeping the default parallelism.</para>
///
/// <para><b>Documented side effect:</b> every successful chain LEAVES its rows behind (one
/// PurchaseReceipt, one PurchaseInvoice and their append-only stock/GL rows) - the ledger can never
/// be cleaned up by hand (Constitution III.2). Rejected bills write nothing, so assertions on the
/// rejected paths compare snapshots taken by the very same test.</para>
///
/// <para><b>Seed data</b> comes from scripts/seed-dev-buying.sql and scripts/seed-dev-stock.sql:
/// supplier <c>SUP-001</c>, item <c>IT-001</c> (FIFO), warehouse <c>WH-01</c> posting to 1310, and
/// the company defaults 2110 / 1130 / 2120 with an open (unfrozen) fiscal period.</para>
/// </remarks>
[Collection(LedgerMutatingCollection.Name)]
public class PurchaseInvoiceOverbillingApiTests : IClassFixture<ErpApiFactory>
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

    private const decimal Rate = 100m;

    /// <summary>Wire code of the rejection (PurchaseErrorCodes.OverbillingNotAllowed).</summary>
    private const string OverbillingCode = "overbilling_not_allowed";

    /// <summary>Spec BY-03 literal: 10 received, nothing billed yet, 15 requested.</summary>
    private const string By03Message = "Cannot bill 15 units. Maximum receivable: 10";

    /// <summary>Spec BY-06 literal: 20 received, 15 billed, 15 requested again.</summary>
    private const string By06Message = "Only 5 units remaining to bill, 15 requested";

    /// <summary>Spec BY-06 shape once the receipt is fully billed: nothing remains.</summary>
    private const string By06NoRemainingMessage = "Only 0 units remaining to bill, 1 requested";

    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    private readonly ErpApiFactory _factory;

    public PurchaseInvoiceOverbillingApiTests(ErpApiFactory factory) => _factory = factory;

    // ------------------------------------------------------------------------------ BY-03

    /// <summary>
    /// Spec BY-03 / tasks.md 4.4: a receipt of 10 units cannot be billed for 15. The API answers
    /// <b>400</b> with code <c>overbilling_not_allowed</c> and the scenario's exact message, and
    /// the general-ledger totals are byte-identical before and after the rejected POST - "no
    /// ledger entries are created" proven against GLEntry itself, not only against the response.
    /// </summary>
    [Fact]
    public async Task Create_BillAboveReceivedQuantity_Returns400By03AndWritesNoLedgerRow()
    {
        using var client = CreateClient();

        var receiptLineId = await CreateReceiptAsync(client, qty: 10m);

        // Snapshot AFTER the receipt (its accrual rows are expected) and BEFORE the bill.
        var before = await ReadLedgerTotalsAsync(client);

        using var response = await PostRawAsync(
            client,
            "/api/v1/purchaseinvoices",
            BuildInvoicePayload(BillNumber(), receiptLineId, qty: 15m),
            Guid.NewGuid().ToString("N"));

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal(OverbillingCode, problem["code"]!.GetValue<string>());
        Assert.Equal("Overbilling Not Allowed", problem["title"]!.GetValue<string>());
        Assert.Equal(By03Message, problem["detail"]!.GetValue<string>());

        // BY-03: the rejected bill booked NOTHING - the full-set totals did not move.
        var after = await ReadLedgerTotalsAsync(client);
        Assert.Equal(before.TotalDebit, after.TotalDebit);
        Assert.Equal(before.TotalCredit, after.TotalCredit);
    }

    // ------------------------------------------------------------------------------ BY-06

    /// <summary>
    /// Spec BY-06 / tasks.md 4.7: two AP clerks bill 15 units each, CONCURRENTLY, against one
    /// receipt of 20. Row locking on the receipt line serializes them: exactly ONE 201 and one 400
    /// carrying <c>overbilling_not_allowed</c> with the BY-06 literal. The receipt then still
    /// accepts the remaining 5 units (progressive billing end to end - no unique-index backstop),
    /// and a further bill of 1 unit is rejected with the remaining-is-zero form of the same message.
    /// </summary>
    [Fact]
    public async Task Create_ConcurrentBillsAgainstOneReceipt_LetsExactlyOneThroughAndBlocksTheRest()
    {
        using var client = CreateClient();

        var receiptLineId = await CreateReceiptAsync(client, qty: 20m);
        var url = "/api/v1/purchaseinvoices";

        // Two distinct clerks: distinct Idempotency-Keys AND distinct bill numbers/payloads, so
        // the idempotency guard classifies them as two independent requests (spec BY-04 territory)
        // and the only serialization left is the domain row lock of spec BY-06.
        var clerkTasks = new[]
        {
            PostRawAsync(
                client, url,
                BuildInvoicePayload(BillNumber(), receiptLineId, qty: 15m),
                Guid.NewGuid().ToString("N")),
            PostRawAsync(
                client, url,
                BuildInvoicePayload(BillNumber(), receiptLineId, qty: 15m),
                Guid.NewGuid().ToString("N")),
        };

        var responses = await Task.WhenAll(clerkTasks);

        // Read every body BEFORE disposing (the tasks share the connection pool).
        var payloads = new List<(HttpStatusCode Status, string Body)>(responses.Length);
        foreach (var response in responses)
        {
            payloads.Add((response.StatusCode, await response.Content.ReadAsStringAsync()));
        }

        foreach (var response in responses)
        {
            response.Dispose();
        }

        var winners = payloads.Where(p => p.Status == HttpStatusCode.Created).ToList();
        var losers = payloads.Where(p => p.Status == HttpStatusCode.BadRequest).ToList();

        Assert.True(
            winners.Count == 1 && losers.Count == 1,
            "Exactly one clerk must win the row lock: statuses = "
            + string.Join(", ", payloads.Select(p => (int)p.Status))
            + "; bodies = " + string.Join(" | ", payloads.Select(p => p.Body)));

        var problem = JsonNode.Parse(losers[0].Body)!;
        Assert.Equal(OverbillingCode, problem["code"]!.GetValue<string>());
        Assert.Equal(By06Message, problem["detail"]!.GetValue<string>());

        // The winner is a real posted bill (spec BY-05's birth state).
        var winner = JsonNode.Parse(winners[0].Body)!["invoice"]!;
        Assert.Equal("Unpaid", winner["status"]!.GetValue<string>());

        // Progressive billing still works: the remaining 5 units bill successfully (201), which
        // proves the old one-invoice-per-receipt unique index is really gone.
        using var remainder = await PostRawAsync(
            client,
            url,
            BuildInvoicePayload(BillNumber(), receiptLineId, qty: 5m),
            Guid.NewGuid().ToString("N"));

        var remainderBytes = await remainder.Content.ReadAsByteArrayAsync();
        Assert.True(
            remainder.StatusCode == HttpStatusCode.Created,
            $"Remainder POST {(int)remainder.StatusCode}: {Encoding.UTF8.GetString(remainderBytes)}");

        var remainderInvoice = JsonNode.Parse(Encoding.UTF8.GetString(remainderBytes))!["invoice"]!;
        Assert.Equal(500m, remainderInvoice["grandTotal"]!.GetValue<decimal>());
        Assert.Equal("Unpaid", remainderInvoice["status"]!.GetValue<string>());

        // The receipt is now FULLY billed: any further bill is the remaining-is-zero rejection.
        using var exhausted = await PostRawAsync(
            client,
            url,
            BuildInvoicePayload(BillNumber(), receiptLineId, qty: 1m),
            Guid.NewGuid().ToString("N"));

        var exhaustedProblem = await AssertProblemAsync(exhausted, HttpStatusCode.BadRequest);
        Assert.Equal(OverbillingCode, exhaustedProblem["code"]!.GetValue<string>());
        Assert.Equal(By06NoRemainingMessage, exhaustedProblem["detail"]!.GetValue<string>());
    }

    // ------------------------------------------------------------------------------ helpers

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
    /// Posts a ONE-line receipt of <paramref name="qty"/> units at <see cref="Rate"/> (no purchase
    /// order) and returns the id of its receipt line - the anchor every invoice below bills against.
    /// </summary>
    private async Task<Guid> CreateReceiptAsync(HttpClient client, decimal qty)
    {
        var payload = SerializePayload(new
        {
            companyId = ErpApiFactory.DevCompanyId,
            warehouseId = WarehouseId,
            supplierId = SupplierId,
            postingDate = Format(DateOnly.FromDateTime(DateTime.UtcNow)),
            lines = new[] { new { itemId = ItemId, qty, rate = Rate } },
        });

        using var response = await PostRawAsync(
            client, "/api/v1/purchasereceipts", payload, Guid.NewGuid().ToString("N"));

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Receipt POST {(int)response.StatusCode}: {Encoding.UTF8.GetString(bytes)}");

        var receiptPosting = JsonNode.Parse(Encoding.UTF8.GetString(bytes))!;
        return receiptPosting["receipt"]!["lines"]![0]!["id"]!.GetValue<Guid>();
    }

    /// <summary>
    /// The invoice POST body: one line billing <paramref name="qty"/> units of the given receipt
    /// line at <see cref="Rate"/>. The tax defaults to 0 so the grand total is exactly
    /// <c>qty * Rate</c> (assertions read it back as a plain number).
    /// </summary>
    private static string BuildInvoicePayload(
        string billNumber,
        Guid receiptLineId,
        decimal qty,
        decimal taxAmount = 0m)
    {
        var postingDate = DateOnly.FromDateTime(DateTime.UtcNow);

        return SerializePayload(new
        {
            companyId = ErpApiFactory.DevCompanyId,
            supplierId = SupplierId,
            billNumber,
            postingDate = Format(postingDate),
            dueDate = Format(postingDate.AddDays(30)),
            taxAmount,
            lines = new[]
            {
                new
                {
                    purchaseReceiptLineId = receiptLineId,
                    itemId = ItemId,
                    qty,
                    rate = Rate,
                },
            },
        });
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
    /// Debit/Credit totals of the FULL general-ledger set of the dev company - the "no ledger
    /// entry was created" oracle of spec BY-03. The report computes them over every matching row,
    /// never over the returned page, so they move the moment a posting commits.
    /// </summary>
    private async Task<(decimal TotalDebit, decimal TotalCredit)> ReadLedgerTotalsAsync(HttpClient client)
    {
        using var response = await client.GetAsync(
            $"/api/v1/FinancialReports/general-ledger?companyId={ErpApiFactory.DevCompanyId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var report = (JsonObject)JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        return (
            report["totalDebit"]!.GetValue<decimal>(),
            report["totalCredit"]!.GetValue<decimal>());
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
}
