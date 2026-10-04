using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Erp.Api.IntegrationTests;

/// <summary>
/// Task 3.9 acceptance, end to end over HTTP: adversarial multi-threaded issues against a LOW
/// on-hand quantity. The posting engine must serialize concurrent consumers of the same Kardex
/// range so exactly the available units are issued, every excess request is rejected with
/// 400 <c>insufficient_stock</c> (the Task 3.3 <see cref="Erp.Domain.Exceptions.InsufficientStockException"/>
/// reaching the caller) and the warehouse never goes negative.
/// </summary>
/// <remarks>
/// <para><b>Why the LedgerMutating collection.</b> Every voucher here appends StockLedgerEntry AND
/// GLEntry rows, and the baseline read through GET /items must not race another class mutating the
/// Kardex - the same reason the delivery-note, journal and financial-report classes share it.</para>
///
/// <para><b>Provisioning stays neutral.</b> Each test receipts exactly what it is about to issue
/// (test 1 additionally restores the pre-existing balance it drains, EVEN when an assertion
/// fails), so the seeded IT-001 on-hand in WH-01 lands back on its baseline. The ledger rows
/// themselves stay: Constitution Article III.2 makes the Kardex append-only.</para>
/// </remarks>
[Collection(LedgerMutatingCollection.Name)]
public class StockEntriesConcurrencyApiTests : IClassFixture<ErpApiFactory>
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";
    private const string StockEntriesPath = "/api/v1/stockentries";

    /// <summary>scripts/seed-dev-stock.sql: IT-001 Steel Bracket - the seeded FIFO item.</summary>
    private static readonly Guid ItemId = Guid.Parse("c0000000-0000-4000-8000-000000000001");

    /// <summary>scripts/seed-dev-stock.sql: WH-01 Main Stores.</summary>
    private static readonly Guid WarehouseId = Guid.Parse("d0000000-0000-4000-8000-000000000002");

    /// <summary>Units every test receipts first, so the available quantity is never zero.</summary>
    private const decimal RestockQty = 10m;

    /// <summary>Concurrent attackers - enough threads to expose a lost-update race.</summary>
    private const int Attackers = 8;

    /// <summary>Stamp of the run; every voucher in both tests posts on this date.</summary>
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly ErpApiFactory _factory;

    public StockEntriesConcurrencyApiTests(ErpApiFactory factory) => _factory = factory;

    // ------------------------------------------------------------------- Task 3.9 acceptance

    /// <summary>
    /// The oversell attack: <see cref="Attackers"/> threads each try to issue EVERYTHING that is
    /// on hand at the same instant. Exactly ONE may win - it issues exactly the available units -
    /// and every excess request must come back as 400 <c>insufficient_stock</c>. The final balance
    /// must be zero: a second winner or a negative quantity means two transactions consumed the
    /// same units (the race a plain FIFO read cannot close).
    /// </summary>
    [Fact]
    public async Task ConcurrentIssuesOfEverythingAvailable_ExactlyOneWinsAndTheRestAreRejected()
    {
        using var client = CreateClient(ErpApiFactory.DevTenantId);
        await ReceiveStockAsync(client, RestockQty);

        var available = await ReadOnHandAsync(client);
        Assert.True(
            available >= RestockQty,
            $"The receipt must lift the on-hand quantity (found {available}).");

        // What the warehouse held BEFORE this test: the finally block restores exactly this, so a
        // failed adversarial run cannot leave the dev warehouse oversold for the next run.
        var baseline = available - RestockQty;

        var responses = Array.Empty<HttpResponseMessage>();
        try
        {
            var stamp = System.Diagnostics.Stopwatch.StartNew();
            var timeline = new (long Start, long End)[Attackers];
            responses = await Task.WhenAll(
                Enumerable.Range(0, Attackers).Select(i => Task.Run(async () =>
                {
                    var start = stamp.ElapsedMilliseconds;
                    var response = await IssueAsync(client, available);
                    timeline[i] = (start, stamp.ElapsedMilliseconds);
                    return response;
                })));

            var winners = new List<HttpResponseMessage>();
            var rejectedCodes = new List<string>();

            foreach (var response in responses)
            {
                if (response.StatusCode == HttpStatusCode.Created)
                {
                    winners.Add(response);
                    continue;
                }

                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
                rejectedCodes.Add(problem["code"]!.GetValue<string>());
            }

            // Exactly the available units are issued: ONE winner drains the warehouse, every excess
            // request is the Task 3.3 rejection carrying the stable machine code.
            Assert.Single(winners);
            Assert.Equal(Attackers - 1, rejectedCodes.Count);
            Assert.All(rejectedCodes, code => Assert.Equal("insufficient_stock", code));

            // The overselling oracle: negative on-hand (or a second winner) means the concurrent
            // consumers were NOT serialized on the Kardex range.
            Assert.Equal(0m, await ReadOnHandAsync(client));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }

            var current = await ReadOnHandAsync(client);
            if (current < baseline)
            {
                await ReceiveStockAsync(client, baseline - current);
            }
        }
    }

    /// <summary>
    /// The throughput half of Task 3.9: six threads each issue ONE unit out of a stock that can
    /// absorb all of them. Serialization must not turn into false rejections (all six win), must
    /// consume EXACTLY six units in total (no lost update under contention) and must hand out six
    /// DISTINCT <c>MI-YYYY-NNNNN</c> vouchers (the gapless numbering of Constitution III.4 racing).
    /// </summary>
    [Fact]
    public async Task ConcurrentSmallIssues_AllSucceedAndConsumeExactlySixUnits()
    {
        using var client = CreateClient(ErpApiFactory.DevTenantId);

        // Exactly six units are issued below, so this provisioning is neutral by construction.
        await ReceiveStockAsync(client, 6m);
        var available = await ReadOnHandAsync(client);
        Assert.True(available >= 6m, $"The receipt must lift the on-hand quantity (found {available}).");

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 6).Select(_ => IssueAsync(client, 1m)));

        var voucherNos = new List<string>();
        foreach (var response in responses)
        {
            using (response)
            {
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
                var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
                voucherNos.Add(body["entry"]!["voucherNo"]!.GetValue<string>());
            }
        }

        Assert.Equal(6, voucherNos.Distinct().Count());
        Assert.Equal(available - 6m, await ReadOnHandAsync(client));
    }

    // ---------------------------------------------------------------------------------- helpers

    /// <summary>Tenant-scoped client: only X-Tenant-ID (see ErpApiFactory remarks - auth is NOT disabled).</summary>
    private HttpClient CreateClient(Guid tenantId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-ID", tenantId.ToString());
        return client;
    }

    /// <summary>
    /// Posts a MaterialReceipt of <paramref name="quantity"/> IT-001 into WH-01 so the concurrent
    /// issues below start from a known non-zero availability (a fresh Idempotency-Key per call,
    /// Constitution VI.4).
    /// </summary>
    private static async Task ReceiveStockAsync(HttpClient client, decimal quantity)
    {
        var payload = new
        {
            companyId = ErpApiFactory.DevCompanyId,
            entryType = "MaterialReceipt", // JsonStringEnumConverter is registered in Program.cs
            warehouseId = WarehouseId,
            postingDate = Iso(Today),
            lines = new[] { new { itemId = ItemId, qty = quantity, rate = 10m } },
        };

        using var response = await PostAsync(client, payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    /// <summary>
    /// Fires ONE MaterialIssue of <paramref name="quantity"/> units and returns the pending
    /// response - the task itself, so the caller can run many of them truly CONCURRENTLY with
    /// <c>Task.WhenAll</c> instead of awaiting them one by one.
    /// </summary>
    private static Task<HttpResponseMessage> IssueAsync(HttpClient client, decimal quantity)
    {
        var payload = new
        {
            companyId = ErpApiFactory.DevCompanyId,
            entryType = "MaterialIssue", // JsonStringEnumConverter is registered in Program.cs
            warehouseId = WarehouseId,
            postingDate = Iso(Today),
            lines = new[] { new { itemId = ItemId, qty = quantity } },
        };

        return PostAsync(client, payload);
    }

    /// <summary>POSTs one stock voucher with a FRESH Idempotency-Key (Constitution VI.4).</summary>
    private static Task<HttpResponseMessage> PostAsync(HttpClient client, object payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, StockEntriesPath)
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add(IdempotencyKeyHeader, Guid.NewGuid().ToString("N"));

        return client.SendAsync(request);
    }

    /// <summary>
    /// Current IT-001 on-hand quantity in WH-01 through the item list oracle
    /// (GET /api/v1/items: per-warehouse SUM of the Kardex rows).
    /// </summary>
    private static async Task<decimal> ReadOnHandAsync(HttpClient client)
    {
        using var response = await client.GetAsync(
            $"/api/v1/items?companyId={ErpApiFactory.DevCompanyId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsArray();
        var item = items.Single(node => string.Equals(
            node!["id"]!.GetValue<string>(),
            ItemId.ToString(),
            StringComparison.OrdinalIgnoreCase));

        var stock = item!["stock"]!.AsArray().Single(node => string.Equals(
            node!["warehouseId"]!.GetValue<string>(),
            WarehouseId.ToString(),
            StringComparison.OrdinalIgnoreCase));

        return stock!["qty"]!.GetValue<decimal>();
    }

    private static string Iso(DateOnly date) =>
        date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
