using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Erp.Api.IntegrationTests;

/// <summary>
/// Task 3.7 / spec ST-04 acceptance, end to end over HTTP: cancelling a posted stock voucher
/// through <c>POST /api/v1/stockentries/{id}/cancel</c> must restore the on-hand balance and the
/// financial accounts to the PRE-TRANSACTION state while keeping every original row in place -
/// Constitution III.2/III.3 forbid UPDATE/DELETE on the ledgers, so the only legal correction is
/// a pair of appended compensating rows.
/// </summary>
/// <remarks>
/// <para><b>Provisioning is neutral.</b> Each test receipts exactly the quantity its cancellation
/// then undoes (receipt +5, reversal -5), so the seeded IT-001 on-hand in WH-01 lands back on its
/// baseline. The ledger rows themselves stay: they are the history ST-04 says must survive.</para>
/// </remarks>
[Collection(LedgerMutatingCollection.Name)]
public class StockEntriesCancellationApiTests : IClassFixture<ErpApiFactory>
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";
    private const string StockEntriesPath = "/api/v1/stockentries";

    /// <summary>scripts/seed-dev-stock.sql: IT-001 Steel Bracket - the seeded FIFO item.</summary>
    private static readonly Guid ItemId = Guid.Parse("c0000000-0000-4000-8000-000000000001");

    /// <summary>scripts/seed-dev-stock.sql: WH-01 Main Stores.</summary>
    private static readonly Guid WarehouseId = Guid.Parse("d0000000-0000-4000-8000-000000000002");

    /// <summary>Units every test receipts first; its cancellation restores the baseline again.</summary>
    private const decimal ReceiptQty = 5m;

    private const decimal ReceiptRate = 10m;

    /// <summary>Stamp of the run; every voucher here posts on this date.</summary>
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly ErpApiFactory _factory;

    public StockEntriesCancellationApiTests(ErpApiFactory factory) => _factory = factory;

    // ------------------------------------------------------------------- Task 3.7 acceptance

    /// <summary>
    /// ST-04 in one test: post a receipt, cancel it, and prove - through the DATABASE, not the
    /// response body - that on-hand is back to the pre-transaction value, the original rows are
    /// still there untouched, the compensating rows were appended, and every affected account
    /// nets to zero in the General Ledger.
    /// </summary>
    [Fact]
    public async Task Cancel_PostedReceipt_RestoresOnHandKeepsOriginalRowsAndBalancesGl()
    {
        using var client = CreateClient();
        var onHandBefore = await ReadOnHandAsync(client);

        var (entryId, voucherNo) = await ReceiveStockAsync(client, ReceiptQty);
        Assert.Equal(onHandBefore + ReceiptQty, await ReadOnHandAsync(client));

        // Pre-cancel bookkeeping: one original Kardex row, a balanced original GL pair.
        var kardex = await ReadKardexAsync(voucherNo);
        Assert.Single(kardex);
        Assert.Equal(ReceiptQty, kardex[0].QtyChange);
        Assert.False(kardex[0].IsCancelled);

        var (glCount, glDebit, glCredit) = await ReadGlTotalsAsync(entryId);
        Assert.Equal(2, glCount);
        Assert.Equal(glDebit, glCredit);

        using var cancelResponse = await CancelAsync(client, entryId);
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        // Balance restored to the pre-transaction state (the +5 receipt is undone).
        Assert.Equal(onHandBefore, await ReadOnHandAsync(client));

        // ...without deleting history: the original row is still there, byte-identical, and the
        // negated compensating row was APPENDED next to it.
        kardex = await ReadKardexAsync(voucherNo);
        Assert.Equal(2, kardex.Count);
        Assert.Contains(kardex, r => r.QtyChange == ReceiptQty && !r.IsCancelled);
        Assert.Contains(kardex, r => r.QtyChange == -ReceiptQty && r.IsCancelled);
        Assert.Equal(0m, kardex.Sum(r => r.QtyChange));
        Assert.True(await ReadHeaderCancelledAsync(entryId));

        // GL: two untouched originals + two swapped reversals, every account netting to zero.
        (glCount, glDebit, glCredit) = await ReadGlTotalsAsync(entryId);
        Assert.Equal(4, glCount);
        Assert.Equal(glDebit, glCredit);
        Assert.Equal(2, await CountOriginalGlRowsAsync(entryId)); // originals never mutated
        Assert.Equal(0, await CountImbalancedAccountsAsync(entryId));
    }

    /// <summary>
    /// A second cancellation of the same voucher must be rejected with 409
    /// <c>invalid_status_transition</c> and append NOTHING: exactly the two Kardex rows and four
    /// GL rows the first cancellation produced.
    /// </summary>
    [Fact]
    public async Task Cancel_Twice_Returns409AndAppendsNoSecondReversal()
    {
        using var client = CreateClient();
        var onHandBefore = await ReadOnHandAsync(client);

        var (entryId, voucherNo) = await ReceiveStockAsync(client, ReceiptQty);

        using (var first = await CancelAsync(client, entryId))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        var onHandAfterFirstCancel = await ReadOnHandAsync(client);

        using var second = await CancelAsync(client, entryId);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var problem = JsonNode.Parse(await second.Content.ReadAsStringAsync())!;
        Assert.Equal("invalid_status_transition", problem["code"]!.GetValue<string>());

        // No second reversal: the ledgers are exactly what the first cancellation produced.
        Assert.Equal(onHandAfterFirstCancel, await ReadOnHandAsync(client));
        Assert.Equal(2, (await ReadKardexAsync(voucherNo)).Count);
        Assert.Equal(4, (await ReadGlTotalsAsync(entryId)).Count);
    }

    /// <summary>
    /// Unknown voucher: 404 <c>voucher_not_found</c> (the same RFC 7807 contract the purchase
    /// and journal cancellations expose), with no ledger row written.
    /// </summary>
    [Fact]
    public async Task Cancel_UnknownVoucher_Returns404VoucherNotFound()
    {
        using var client = CreateClient();

        using var response = await CancelAsync(client, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal("voucher_not_found", problem["code"]!.GetValue<string>());
    }

    // ---------------------------------------------------------------------------------- helpers

    /// <summary>Tenant-scoped client: only X-Tenant-ID (see ErpApiFactory remarks - auth is NOT disabled).</summary>
    private HttpClient CreateClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-ID", ErpApiFactory.DevTenantId.ToString());
        return client;
    }

    /// <summary>
    /// Posts a MaterialReceipt of <paramref name="quantity"/> IT-001 into WH-01 and returns the
    /// fresh voucher's database id (looked up by its gapless VoucherNo) plus the VoucherNo itself
    /// (a fresh Idempotency-Key per call, Constitution VI.4).
    /// </summary>
    private static async Task<(Guid EntryId, string VoucherNo)> ReceiveStockAsync(
        HttpClient client,
        decimal quantity)
    {
        var payload = new
        {
            companyId = ErpApiFactory.DevCompanyId,
            entryType = "MaterialReceipt", // JsonStringEnumConverter is registered in Program.cs
            warehouseId = WarehouseId,
            postingDate = Iso(Today),
            lines = new[] { new { itemId = ItemId, qty = quantity, rate = ReceiptRate } },
        };

        using var response = await PostAsync(client, payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var voucherNo = body["entry"]!["voucherNo"]!.GetValue<string>();

        return (await FindEntryIdAsync(voucherNo), voucherNo);
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

    /// <summary>Cancels one stock voucher with a FRESH Idempotency-Key (Constitution VI.4).</summary>
    private static Task<HttpResponseMessage> CancelAsync(HttpClient client, Guid entryId)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{StockEntriesPath}/{entryId}/cancel?companyId={ErpApiFactory.DevCompanyId}");
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

    // ---------------------------------------------------------------------------- SQL oracles

    private sealed record KardexRow(decimal QtyChange, bool IsCancelled);

    /// <summary>Resolves a voucher's database id from its gapless number (input for the GL oracles).</summary>
    private static async Task<Guid> FindEntryIdAsync(string voucherNo)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT [Id] FROM [dbo].[StockEntry] WHERE [VoucherNo] = @VoucherNo;", connection);
        command.Parameters.AddWithValue("@VoucherNo", voucherNo);

        var id = await command.ExecuteScalarAsync();
        Assert.NotNull(id);
        return new Guid(id.ToString()!);
    }

    /// <summary>Every Kardex row of the voucher, originals and reversals alike (append-only view).</summary>
    private static async Task<List<KardexRow>> ReadKardexAsync(string voucherNo)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT [QtyChange], [IsCancelled] FROM [dbo].[StockLedgerEntry] " +
            "WHERE [VoucherNo] = @VoucherNo;", connection);
        command.Parameters.AddWithValue("@VoucherNo", voucherNo);

        var rows = new List<KardexRow>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new KardexRow(reader.GetDecimal(0), reader.GetBoolean(1)));
        }

        return rows;
    }

    /// <summary>Row count plus debit/credit totals of every GL row stamped with the voucher id.</summary>
    private static async Task<(int Count, decimal Debit, decimal Credit)> ReadGlTotalsAsync(Guid entryId)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT COUNT(*), COALESCE(SUM([Debit]), 0), COALESCE(SUM([Credit]), 0) " +
            "FROM [dbo].[GLEntry] WHERE [VoucherId] = @Id;", connection);
        command.Parameters.AddWithValue("@Id", entryId);

        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (reader.GetInt32(0), reader.GetDecimal(1), reader.GetDecimal(2));
    }

    /// <summary>GL rows of the voucher still marked as ORIGINAL (a mutated row would drop out).</summary>
    private static async Task<int> CountOriginalGlRowsAsync(Guid entryId)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT COUNT(*) FROM [dbo].[GLEntry] " +
            "WHERE [VoucherId] = @Id AND [IsCancelled] = 0;", connection);
        command.Parameters.AddWithValue("@Id", entryId);

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    /// <summary>
    /// Accounts touched by the voucher whose debits and credits do NOT cancel out - zero after a
    /// cancellation: original + reversal net to nothing on every account (ST-04's "financial
    /// accounts are restored").
    /// </summary>
    private static async Task<int> CountImbalancedAccountsAsync(Guid entryId)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT COUNT(*) FROM (" +
            "    SELECT [AccountId] FROM [dbo].[GLEntry] WHERE [VoucherId] = @Id " +
            "    GROUP BY [AccountId] HAVING ABS(SUM([Debit]) - SUM([Credit])) > 0.0001" +
            ") AS Imbalanced;", connection);
        command.Parameters.AddWithValue("@Id", entryId);

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    /// <summary>The voucher header's IsCancelled flag, read straight from the database.</summary>
    private static async Task<bool> ReadHeaderCancelledAsync(Guid entryId)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT [IsCancelled] FROM [dbo].[StockEntry] WHERE [Id] = @Id;", connection);
        command.Parameters.AddWithValue("@Id", entryId);

        return Convert.ToBoolean(await command.ExecuteScalarAsync());
    }
}
