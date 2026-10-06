using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Erp.Api.IntegrationTests;

/// <summary>
/// Tasks 9.6/9.7 live: the full MF-02 transfer + MF-03 manufacture cycle with its exact dollar
/// pairs, the MF-04 idempotent manufacture replay, the MF-05 cancel/reversal, the MF-06
/// concurrent-transfer race, and the transitive BOM-cycle closure at submit - end to end over
/// HTTP against the LIVE dev container.
/// </summary>
/// <remarks>
/// <para><b>The oracle is the DATABASE, not only the response.</b> Every test snapshots the
/// global <c>StockLedgerEntry</c>/<c>GLEntry</c> counts around the ACT and asserts DELTAS -
/// never absolute totals, which other tests legitimately append to (the FiscalPeriodLock /
/// banking pattern). Serialized through <see cref="LedgerMutatingCollection"/> for the same
/// reason.</para>
/// <para><b>Masters are per-test rows, never shared.</b> Items, warehouses, BOMs and work
/// orders use fresh GUIDs (and GUID-suffixed codes, which must stay tenant-unique) on every
/// run, so re-runs stay hermetic without cleanup. Ledger rows (StockEntry/SLE/GLEntry) stay
/// behind like every other posting test leaves them: <c>GLEntry</c> is append-only
/// (application rule plus the <c>trg_GLEntry_AppendOnly</c> trigger), so voucher masters that
/// were posted against cannot be deleted afterwards. Shared seeded rows (UOM Each, accounts
/// 1310/1320/1330, WS-01 from scripts/seed-dev-manufacturing.sql) are READ-ONLY here.</para>
/// <para><b>Provisioning stays neutral where asserted.</b> Receipts exactly cover what the
/// flow consumes; the cancel test's reversal and the race test's winner-cancel + issue drain
/// every per-item balance back to zero, so no test leaves usable stock behind.</para>
/// <para><b>No BOM-CRUD endpoints exist (out of scope).</b> BOM and operation rows are
/// inserted via direct SQL setup with deterministic per-test GUIDs (the banking per-test-rows
/// pattern); the work-order flow itself (create/submit/transfer/complete/cancel) always goes
/// through the real API.</para>
/// </remarks>
[Collection(LedgerMutatingCollection.Name)]
public class WorkOrderManufacturingApiTests : IClassFixture<ErpApiFactory>
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    private static readonly Guid EachUomId = Guid.Parse("b0000000-0000-4000-8000-000000000001");
    private static readonly Guid StoresAccountId = Guid.Parse("a0000000-0000-4000-8000-000000001310");
    private static readonly Guid WipAccountId = Guid.Parse("a0000000-0000-4000-8000-000000001320");
    private static readonly Guid FinishedAccountId = Guid.Parse("a0000000-0000-4000-8000-000000001330");
    private static readonly Guid AbsorptionAccountId = Guid.Parse("a0000000-0000-4000-8000-000000005210");
    private static readonly Guid WarehouseRootId = Guid.Parse("d0000000-0000-4000-8000-000000000001");
    private static readonly Guid WorkstationWs01Id = Guid.Parse("e0000000-0000-4000-8000-000000000001");

    private static readonly string Today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

    private readonly ErpApiFactory _factory;

    public WorkOrderManufacturingApiTests(ErpApiFactory factory) => _factory = factory;

    // ------------------------------------------------- MF-02 + MF-03 full cycle live

    /// <summary>
    /// MF-02 + MF-03 verbatim: 10 assemblies move 20xA @ $15 + 10xB @ $20 Stores -&gt; WIP
    /// (Dr 1320 $500 / Cr 1310 $500), then complete into 10 FG @ $70 (Dr 1330 $700 / Cr 1320
    /// $500 / Cr 5210 $200). Stores and WIP net to zero across the run; FG/absorption carry
    /// the capitalized balances; row deltas are exact (7 SLE + 7 GL).
    /// </summary>
    [Fact]
    public async Task FullCycle_TransferThenManufacture_PostsExactPairsAndCompletes()
    {
        using var client = CreateClient();
        var tag = Guid.NewGuid().ToString("N")[..8];

        var itemA = await CreateItemAsync(client, $"MFG-{tag}-A", 15m);
        var itemB = await CreateItemAsync(client, $"MFG-{tag}-B", 20m);
        var itemFg = await CreateItemAsync(client, $"MFG-{tag}-FG", 0m);
        var stores = await CreateWarehouseAsync(client, $"MFG-{tag}-STORES", StoresAccountId);
        var wip = await CreateWarehouseAsync(client, $"MFG-{tag}-WIP", WipAccountId);
        var fg = await CreateWarehouseAsync(client, $"MFG-{tag}-FG", FinishedAccountId);

        var receiptA = await ReceiveAsync(client, stores, itemA, 20m, 15m);
        var receiptB = await ReceiveAsync(client, stores, itemB, 10m, 20m);
        var bomId = Guid.NewGuid();
        await InsertBomAsync(
            bomId, $"MFG-BOM-{tag}", itemFg, 1m,
            new[] { (Guid.NewGuid(), itemA, 2m, 15m, 30m), (Guid.NewGuid(), itemB, 1m, 20m, 20m) },
            new[] { (Guid.NewGuid(), WorkstationWs01Id, "Assembly", 30m) });

        var (sleBefore, glBefore) = (await CountSLEAsync(), await CountGLEntryAsync());

        var orderId = await CreateWorkOrderAsync(client, itemFg, bomId, 10m, stores, wip, fg);
        await SubmitWorkOrderAsync(client, orderId);

        using var transfer = await PostTransferAsync(client, orderId);
        Assert.Equal(HttpStatusCode.Created, transfer.StatusCode);
        var transferBody = JsonNode.Parse(await transfer.Content.ReadAsStringAsync())!;
        Assert.Equal("MaterialTransfer", transferBody["entry"]!["entryType"]!.GetValue<string>());
        var transferId = transferBody["entry"]!["id"]!.GetValue<Guid>();
        AssertTransferGl(transferBody, debit1320: 500m, credit1310: 500m);

        using var complete = await PostCompleteAsync(client, orderId, 10m);
        Assert.Equal(HttpStatusCode.Created, complete.StatusCode);
        var completeBody = JsonNode.Parse(await complete.Content.ReadAsStringAsync())!;
        Assert.Equal("Manufacture", completeBody["entry"]!["entryType"]!.GetValue<string>());
        var completeId = completeBody["entry"]!["id"]!.GetValue<Guid>();

        // Finished goods: +10 units @ $70.00 = $700.00.
        var fgLine = Assert.Single(completeBody["entry"]!["lines"]!.AsArray());
        Assert.Equal(10m, fgLine!["qty"]!.GetValue<decimal>());
        Assert.Equal(70m, fgLine!["rate"]!.GetValue<decimal>());

        // The EXACT MF-03 triple.
        var gl = completeBody["glEntries"]!.AsArray();
        Assert.Equal(700m, Sum(gl, "1330", "debit"));
        Assert.Equal(500m, Sum(gl, "1320", "credit"));
        Assert.Equal(200m, Sum(gl, "5210", "credit"));
        Assert.Equal(
            completeBody["totalDebit"]!.GetValue<decimal>(),
            completeBody["totalCredit"]!.GetValue<decimal>());

        Assert.Equal("Completed", await ReadOrderStatusAsync(client, orderId));
        Assert.Equal(10m, await ReadOnHandAsync(client, itemFg, fg));

        // Exact row deltas: transfer (4 SLE + 4 GL) + manufacture (3 SLE + 3 GL).
        Assert.Equal(sleBefore + 7, await CountSLEAsync());
        Assert.Equal(glBefore + 7, await CountGLEntryAsync());

        // Per-account nets across ALL run vouchers (receipts + transfer + manufacture):
        // Stores and WIP wash out, FG and absorption carry the capitalized balances.
        var voucherIds = new[] { receiptA, receiptB, transferId, completeId };
        Assert.Equal(0m, await ReadAccountNetAsync(voucherIds, StoresAccountId));
        Assert.Equal(0m, await ReadAccountNetAsync(voucherIds, WipAccountId));
        Assert.Equal(700m, await ReadAccountNetAsync(voucherIds, FinishedAccountId));
        Assert.Equal(-200m, await ReadAccountNetAsync(voucherIds, AbsorptionAccountId));

        // Temporal history is enabled: the BOM is visible through SYSTEM_TIME.
        Assert.True(await BomVisibleThroughHistoryAsync(bomId));
    }

    // ------------------------------------------------------------- MF-04 replay live

    /// <summary>
    /// MF-04 live: completing with the same <c>Idempotency-Key</c> twice returns 200 with the
    /// ORIGINAL 201 body byte-identical, and appends zero StockLedgerEntry/GLEntry rows.
    /// Idempotent replay needs no new backend code - this test PROVES the guard holds.
    /// </summary>
    [Fact]
    public async Task ManufactureReplay_SameKeyTwice_SecondIs200ByteIdenticalWithZeroNewRows()
    {
        using var client = CreateClient();
        var tag = Guid.NewGuid().ToString("N")[..8];

        var itemA = await CreateItemAsync(client, $"MFG-{tag}-A", 15m);
        var itemB = await CreateItemAsync(client, $"MFG-{tag}-B", 20m);
        var itemFg = await CreateItemAsync(client, $"MFG-{tag}-FG", 0m);
        var stores = await CreateWarehouseAsync(client, $"MFG-{tag}-STORES", StoresAccountId);
        var wip = await CreateWarehouseAsync(client, $"MFG-{tag}-WIP", WipAccountId);
        var fg = await CreateWarehouseAsync(client, $"MFG-{tag}-FG", FinishedAccountId);

        await ReceiveAsync(client, stores, itemA, 2m, 15m);
        await ReceiveAsync(client, stores, itemB, 1m, 20m);

        var bomId = Guid.NewGuid();
        await InsertBomAsync(
            bomId, $"MFG-BOM-{tag}", itemFg, 1m,
            new[] { (Guid.NewGuid(), itemA, 2m, 15m, 30m), (Guid.NewGuid(), itemB, 1m, 20m, 20m) },
            new[] { (Guid.NewGuid(), WorkstationWs01Id, "Assembly", 30m) });

        var orderId = await CreateWorkOrderAsync(client, itemFg, bomId, 1m, stores, wip, fg);
        await SubmitWorkOrderAsync(client, orderId);
        using (var transfer = await PostTransferAsync(client, orderId))
        {
            Assert.Equal(HttpStatusCode.Created, transfer.StatusCode);
        }

        var key = Guid.NewGuid();
        using var first = await PostCompleteAsync(client, orderId, 1m, key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstBytes = await first.Content.ReadAsByteArrayAsync();

        var (sleBefore, glBefore) = (await CountSLEAsync(), await CountGLEntryAsync());

        using var replay = await PostCompleteAsync(client, orderId, 1m, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayBytes = await replay.Content.ReadAsByteArrayAsync();

        Assert.Equal(firstBytes, replayBytes);
        Assert.Equal(sleBefore, await CountSLEAsync());
        Assert.Equal(glBefore, await CountGLEntryAsync());

        Assert.Equal("Completed", await ReadOrderStatusAsync(client, orderId));
        Assert.Equal(1m, await ReadOnHandAsync(client, itemFg, fg));
    }

    // ------------------------------------------------- MF-05 cancel/reversal live

    /// <summary>
    /// MF-05 live: transfer then cancel posts a NEW compensating MT voucher WIP -&gt; Stores,
    /// WIP on-hand returns to pre-transfer (zero for the hermetic items), the two vouchers net
    /// to zero per account, and the order lands Cancelled. Cancel-after-complete is a 409 with
    /// zero new rows - FIFO layers are never unpicked.
    /// </summary>
    [Fact]
    public async Task Cancel_AfterTransfer_ReversesWipToZeroAndCancels()
    {
        using var client = CreateClient();
        var tag = Guid.NewGuid().ToString("N")[..8];

        var itemA = await CreateItemAsync(client, $"MFG-{tag}-A", 15m);
        var itemB = await CreateItemAsync(client, $"MFG-{tag}-B", 20m);
        var itemFg = await CreateItemAsync(client, $"MFG-{tag}-FG", 0m);
        var stores = await CreateWarehouseAsync(client, $"MFG-{tag}-STORES", StoresAccountId);
        var wip = await CreateWarehouseAsync(client, $"MFG-{tag}-WIP", WipAccountId);
        var fg = await CreateWarehouseAsync(client, $"MFG-{tag}-FG", FinishedAccountId);

        await ReceiveAsync(client, stores, itemA, 20m, 15m);
        await ReceiveAsync(client, stores, itemB, 10m, 20m);

        var bomId = Guid.NewGuid();
        await InsertBomAsync(
            bomId, $"MFG-BOM-{tag}", itemFg, 1m,
            new[] { (Guid.NewGuid(), itemA, 2m, 15m, 30m), (Guid.NewGuid(), itemB, 1m, 20m, 20m) },
            new[] { (Guid.NewGuid(), WorkstationWs01Id, "Assembly", 30m) });

        var orderId = await CreateWorkOrderAsync(client, itemFg, bomId, 10m, stores, wip, fg);
        await SubmitWorkOrderAsync(client, orderId);
        Guid transferId;
        using (var transfer = await PostTransferAsync(client, orderId))
        {
            Assert.Equal(HttpStatusCode.Created, transfer.StatusCode);
            transferId = JsonNode.Parse(await transfer.Content.ReadAsStringAsync())!["entry"]!["id"]!.GetValue<Guid>();
        }

        Assert.Equal(20m, await ReadOnHandAsync(client, itemA, wip));
        var (sleBefore, glBefore) = (await CountSLEAsync(), await CountGLEntryAsync());

        using var cancel = await PostCancelAsync(client, orderId);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        var cancelBody = JsonNode.Parse(await cancel.Content.ReadAsStringAsync())!;
        Assert.Equal("Cancelled", cancelBody["status"]!.GetValue<string>());

        // WIP back to pre-transfer: the hermetic items hold zero there again.
        Assert.Equal(0m, await ReadOnHandAsync(client, itemA, wip));
        Assert.Equal(0m, await ReadOnHandAsync(client, itemB, wip));

        // Exactly one compensating voucher: a NEW MT voucher WIP -&gt; Stores (4 SLE + 4 GL).
        Assert.Equal(sleBefore + 4, await CountSLEAsync());
        Assert.Equal(glBefore + 4, await CountGLEntryAsync());
        var reversalId = await ReadNewEntryIdAsync(transferId);
        var reversal = await ReadEntryAsync(reversalId);
        Assert.Equal("MaterialTransfer", reversal["entryType"]!.GetValue<string>());

        // Transfer + reversal net to zero per account.
        Assert.Equal(0m, await ReadAccountNetAsync(new[] { transferId, reversalId }, StoresAccountId));
        Assert.Equal(0m, await ReadAccountNetAsync(new[] { transferId, reversalId }, WipAccountId));

        Assert.Equal("Cancelled", await ReadOrderStatusAsync(client, orderId));
    }

    /// <summary>
    /// MF-05 boundary live: cancelling a Completed order is a 409
    /// <c>invalid_status_transition</c> with zero new ledger rows.
    /// </summary>
    [Fact]
    public async Task Cancel_AfterComplete_Is409WithZeroNewRows()
    {
        using var client = CreateClient();
        var tag = Guid.NewGuid().ToString("N")[..8];

        var itemA = await CreateItemAsync(client, $"MFG-{tag}-A", 15m);
        var itemFg = await CreateItemAsync(client, $"MFG-{tag}-FG", 0m);
        var stores = await CreateWarehouseAsync(client, $"MFG-{tag}-STORES", StoresAccountId);
        var wip = await CreateWarehouseAsync(client, $"MFG-{tag}-WIP", WipAccountId);
        var fg = await CreateWarehouseAsync(client, $"MFG-{tag}-FG", FinishedAccountId);

        await ReceiveAsync(client, stores, itemA, 2m, 15m);

        var bomId = Guid.NewGuid();
        await InsertBomAsync(
            bomId, $"MFG-BOM-{tag}", itemFg, 1m,
            new[] { (Guid.NewGuid(), itemA, 2m, 15m, 30m) },
            Array.Empty<(Guid, Guid, string, decimal)>());

        var orderId = await CreateWorkOrderAsync(client, itemFg, bomId, 1m, stores, wip, fg);
        await SubmitWorkOrderAsync(client, orderId);
        using (var transfer = await PostTransferAsync(client, orderId))
        {
            Assert.Equal(HttpStatusCode.Created, transfer.StatusCode);
        }

        using (var complete = await PostCompleteAsync(client, orderId, 1m))
        {
            Assert.Equal(HttpStatusCode.Created, complete.StatusCode);
        }

        var (sleBefore, glBefore) = (await CountSLEAsync(), await CountGLEntryAsync());

        using var cancel = await PostCancelAsync(client, orderId);
        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
        var problem = JsonNode.Parse(await cancel.Content.ReadAsStringAsync())!;
        Assert.Equal("invalid_status_transition", problem["code"]!.GetValue<string>());

        Assert.Equal(sleBefore, await CountSLEAsync());
        Assert.Equal(glBefore, await CountGLEntryAsync());
        Assert.Equal("Completed", await ReadOrderStatusAsync(client, orderId));
    }

    // ------------------------------------------------------- MF-06 race live

    /// <summary>
    /// MF-06 live: two work orders race to transfer the last 20 units of one component out of
    /// the same Stores. Exactly one wins (201); the loser is a 400
    /// <c>insufficient_stock</c>; Stores never goes negative. The winner is cancelled in the
    /// finally (compensating WIP -&gt; Stores) and the provisioned stock issued back out, so
    /// the hermetic items land back on zero even when an assertion fails.
    /// </summary>
    [Fact]
    public async Task ConcurrentTransfers_ForLastUnits_ExactlyOneWins()
    {
        using var client = CreateClient();
        var tag = Guid.NewGuid().ToString("N")[..8];

        var itemA = await CreateItemAsync(client, $"MFG-{tag}-A", 15m);
        var itemFg = await CreateItemAsync(client, $"MFG-{tag}-FG", 0m);
        var stores = await CreateWarehouseAsync(client, $"MFG-{tag}-STORES", StoresAccountId);
        var wip = await CreateWarehouseAsync(client, $"MFG-{tag}-WIP", WipAccountId);
        var fg = await CreateWarehouseAsync(client, $"MFG-{tag}-FG", FinishedAccountId);

        // Exactly what ONE transfer consumes: the second concurrent transfer must starve.
        await ReceiveAsync(client, stores, itemA, 20m, 15m);

        var bomId = Guid.NewGuid();
        await InsertBomAsync(
            bomId, $"MFG-BOM-{tag}", itemFg, 1m,
            new[] { (Guid.NewGuid(), itemA, 20m, 15m, 300m) },
            Array.Empty<(Guid, Guid, string, decimal)>());

        var orderOne = await CreateWorkOrderAsync(client, itemFg, bomId, 1m, stores, wip, fg);
        var orderTwo = await CreateWorkOrderAsync(client, itemFg, bomId, 1m, stores, wip, fg);
        await SubmitWorkOrderAsync(client, orderOne);
        await SubmitWorkOrderAsync(client, orderTwo);

        Guid? winnerId = null;
        HttpResponseMessage? first = null;
        HttpResponseMessage? second = null;
        try
        {
            // TRULY concurrent: both transfers hit the drained Stores at the same instant.
            // The Kardex range lock serializes them; the loser starves with insufficient_stock.
            var racerOne = Task.Run(() => PostTransferAsync(client, orderOne));
            var racerTwo = Task.Run(() => PostTransferAsync(client, orderTwo));
            first = await racerOne;
            second = await racerTwo;

            var outcomes = new[] { (Response: first, OrderId: orderOne), (Response: second, OrderId: orderTwo) };
            var winners = outcomes.Where(o => o.Response.StatusCode == HttpStatusCode.Created).ToList();
            var losers = outcomes.Where(o => o.Response.StatusCode != HttpStatusCode.Created).ToList();

            Assert.Single(winners);
            var loser = Assert.Single(losers);
            Assert.Equal(HttpStatusCode.BadRequest, loser.Response.StatusCode);
            var problem = JsonNode.Parse(await loser.Response.Content.ReadAsStringAsync())!;
            Assert.Equal("insufficient_stock", problem["code"]!.GetValue<string>());

            winnerId = winners[0].OrderId;

            // The overselling oracle: Stores drained exactly, never negative.
            Assert.Equal(0m, await ReadOnHandAsync(client, itemA, stores));
        }
        finally
        {
            first?.Dispose();
            second?.Dispose();

            // Restore discipline: cancel the winner (WIP -&gt; Stores) and issue the
            // provisioned stock back out, so the hermetic items land on zero.
            if (winnerId is not null)
            {
                using var cancel = await PostCancelAsync(client, winnerId.Value);
            }

            await IssueAsync(client, stores, itemA, await ReadOnHandAsync(client, itemA, stores));
        }
    }

    // ------------------------------------------- 9.7 transitive closure live

    /// <summary>
    /// Task 9.7 live: SQL-seeded A -&gt; B -&gt; A BOM chain rejects the submit with 400
    /// <c>circular_reference</c> and zero new ledger rows.
    /// </summary>
    [Fact]
    public async Task Submit_TransitiveCycleChain_RejectsWithCircularReferenceAndZeroWrites()
    {
        using var client = CreateClient();
        var tag = Guid.NewGuid().ToString("N")[..8];

        var itemA = await CreateItemAsync(client, $"MFG-{tag}-A", 10m);
        var itemB = await CreateItemAsync(client, $"MFG-{tag}-B", 10m);
        var stores = await CreateWarehouseAsync(client, $"MFG-{tag}-STORES", StoresAccountId);
        var wip = await CreateWarehouseAsync(client, $"MFG-{tag}-WIP", WipAccountId);
        var fg = await CreateWarehouseAsync(client, $"MFG-{tag}-FG", FinishedAccountId);

        var bomA = Guid.NewGuid();
        await InsertBomAsync(
            bomA, $"MFG-BOMA-{tag}", itemA, 1m,
            new[] { (Guid.NewGuid(), itemB, 1m, 10m, 10m) },
            Array.Empty<(Guid, Guid, string, decimal)>());
        await InsertBomAsync(
            Guid.NewGuid(), $"MFG-BOMB-{tag}", itemB, 1m,
            new[] { (Guid.NewGuid(), itemA, 1m, 10m, 10m) },
            Array.Empty<(Guid, Guid, string, decimal)>());

        var orderId = await CreateWorkOrderAsync(client, itemA, bomA, 1m, stores, wip, fg);

        var (sleBefore, glBefore) = (await CountSLEAsync(), await CountGLEntryAsync());

        using var submit = await PostSubmitAsync(client, orderId);
        Assert.Equal(HttpStatusCode.BadRequest, submit.StatusCode);
        var problem = JsonNode.Parse(await submit.Content.ReadAsStringAsync())!;
        Assert.Equal("circular_reference", problem["code"]!.GetValue<string>());

        Assert.Equal(sleBefore, await CountSLEAsync());
        Assert.Equal(glBefore, await CountGLEntryAsync());
        Assert.Equal("Draft", await ReadOrderStatusAsync(client, orderId));
    }

    /// <summary>
    /// Task 9.7 live, diamond counterpart: FG consumes X and Y, both consume Z (shared leaf).
    /// No loop exists, so the submit succeeds.
    /// </summary>
    [Fact]
    public async Task Submit_DiamondSharedSubComponent_SubmitsSuccessfully()
    {
        using var client = CreateClient();
        var tag = Guid.NewGuid().ToString("N")[..8];

        var itemF = await CreateItemAsync(client, $"MFG-{tag}-F", 10m);
        var itemX = await CreateItemAsync(client, $"MFG-{tag}-X", 10m);
        var itemY = await CreateItemAsync(client, $"MFG-{tag}-Y", 10m);
        var itemZ = await CreateItemAsync(client, $"MFG-{tag}-Z", 10m);
        var stores = await CreateWarehouseAsync(client, $"MFG-{tag}-STORES", StoresAccountId);
        var wip = await CreateWarehouseAsync(client, $"MFG-{tag}-WIP", WipAccountId);
        var fg = await CreateWarehouseAsync(client, $"MFG-{tag}-FG", FinishedAccountId);

        var bomF = Guid.NewGuid();
        await InsertBomAsync(
            bomF, $"MFG-BOMF-{tag}", itemF, 1m,
            new[] { (Guid.NewGuid(), itemX, 1m, 10m, 10m), (Guid.NewGuid(), itemY, 1m, 10m, 10m) },
            Array.Empty<(Guid, Guid, string, decimal)>());
        await InsertBomAsync(
            Guid.NewGuid(), $"MFG-BOMX-{tag}", itemX, 1m,
            new[] { (Guid.NewGuid(), itemZ, 1m, 10m, 10m) },
            Array.Empty<(Guid, Guid, string, decimal)>());
        await InsertBomAsync(
            Guid.NewGuid(), $"MFG-BOMY-{tag}", itemY, 1m,
            new[] { (Guid.NewGuid(), itemZ, 1m, 10m, 10m) },
            Array.Empty<(Guid, Guid, string, decimal)>());

        var orderId = await CreateWorkOrderAsync(client, itemF, bomF, 1m, stores, wip, fg);

        using var submit = await PostSubmitAsync(client, orderId);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        Assert.Equal("Submitted", await ReadOrderStatusAsync(client, orderId));
    }

    // ---------------------------------------------------------------------------------- helpers

    /// <summary>Tenant-scoped client: only X-Tenant-ID (see ErpApiFactory remarks - auth is NOT disabled).</summary>
    private HttpClient CreateClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-ID", ErpApiFactory.DevTenantId.ToString());
        return client;
    }

    private static async Task<HttpResponseMessage> PostWithKeyAsync(
        HttpClient client, HttpMethod method, string url, Guid? key = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add(IdempotencyKeyHeader, (key ?? Guid.NewGuid()).ToString("N"));
        return await client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> PostSubmitAsync(HttpClient client, Guid orderId) =>
        PostWithKeyAsync(
            client, HttpMethod.Post,
            $"/api/v1/workorders/{orderId}/submit?companyId={ErpApiFactory.DevCompanyId}&pageSize=500");

    private static Task<HttpResponseMessage> PostTransferAsync(HttpClient client, Guid orderId, Guid? key = null) =>
        PostWithKeyAsync(
            client, HttpMethod.Post,
            $"/api/v1/workorders/{orderId}/transfer-to-wip?companyId={ErpApiFactory.DevCompanyId}&postingDate={Today}",
            key);

    private static Task<HttpResponseMessage> PostCompleteAsync(
        HttpClient client, Guid orderId, decimal producedQuantity, Guid? key = null) =>
        PostWithKeyAsync(
            client, HttpMethod.Post,
            $"/api/v1/workorders/{orderId}/complete?companyId={ErpApiFactory.DevCompanyId}&producedQuantity={producedQuantity}&postingDate={Today}",
            key);

    private static Task<HttpResponseMessage> PostCancelAsync(HttpClient client, Guid orderId, Guid? key = null) =>
        PostWithKeyAsync(
            client, HttpMethod.Post,
            $"/api/v1/workorders/{orderId}/cancel?companyId={ErpApiFactory.DevCompanyId}&postingDate={Today}",
            key);

    private static async Task<Guid> CreateItemAsync(HttpClient client, string code, decimal rate)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/items", new
        {
            code,
            name = code,
            valuationMethod = "Fifo",
            baseUOMId = EachUomId,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!["id"]!.GetValue<Guid>();
    }

    private static async Task<Guid> CreateWarehouseAsync(HttpClient client, string code, Guid accountId)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/warehouses", new
        {
            companyId = ErpApiFactory.DevCompanyId,
            code,
            name = code,
            accountId,
            parentWarehouseId = WarehouseRootId,
            isGroup = false,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!["id"]!.GetValue<Guid>();
    }

    private static async Task<Guid> ReceiveAsync(HttpClient client, Guid warehouseId, Guid itemId, decimal qty, decimal rate)
    {
        var payload = new
        {
            companyId = ErpApiFactory.DevCompanyId,
            entryType = "MaterialReceipt",
            warehouseId,
            postingDate = Today,
            lines = new[] { new { itemId, qty, rate } },
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/stockentries")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add(IdempotencyKeyHeader, Guid.NewGuid().ToString("N"));

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!["entry"]!["id"]!.GetValue<Guid>();
    }

    private static async Task IssueAsync(HttpClient client, Guid warehouseId, Guid itemId, decimal qty)
    {
        if (qty <= 0)
        {
            return;
        }

        var payload = new
        {
            companyId = ErpApiFactory.DevCompanyId,
            entryType = "MaterialIssue",
            warehouseId,
            postingDate = Today,
            lines = new[] { new { itemId, qty } },
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/stockentries")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add(IdempotencyKeyHeader, Guid.NewGuid().ToString("N"));

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<Guid> CreateWorkOrderAsync(
        HttpClient client, Guid productionItemId, Guid bomId, decimal quantity,
        Guid stores, Guid wip, Guid fg)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/workorders", new
        {
            companyId = ErpApiFactory.DevCompanyId,
            productionItemId,
            bomId,
            quantityToProduce = quantity,
            sourceWarehouseId = stores,
            wipWarehouseId = wip,
            targetWarehouseId = fg,
            plannedStartDate = Today,
            plannedEndDate = Today,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!["id"]!.GetValue<Guid>();
    }

    private static async Task SubmitWorkOrderAsync(HttpClient client, Guid orderId)
    {
        using var response = await PostSubmitAsync(client, orderId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<string> ReadOrderStatusAsync(HttpClient client, Guid orderId)
    {
        using var response = await client.GetAsync($"/api/v1/workorders?companyId={ErpApiFactory.DevCompanyId}&pageSize=500");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var orders = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["items"]!.AsArray();
        var order = orders.Single(n => string.Equals(
            n!["id"]!.GetValue<string>(), orderId.ToString(), StringComparison.OrdinalIgnoreCase));
        return order!["status"]!.GetValue<string>();
    }

    private static async Task<decimal> ReadOnHandAsync(HttpClient client, Guid itemId, Guid warehouseId)
    {
        // Pages through the item list: the dev database accumulates rows across runs, so no
        // fixed page can guarantee the item (Standard Pagination Pattern consequence).
        for (var page = 1; ; page++)
        {
            using var response = await client.GetAsync(
                $"/api/v1/items?companyId={ErpApiFactory.DevCompanyId}&page={page}&pageSize=500");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var items = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["items"]!.AsArray();
            var item = items.SingleOrDefault(n => string.Equals(
                n!["id"]!.GetValue<string>(), itemId.ToString(), StringComparison.OrdinalIgnoreCase));
            if (item is not null)
            {
                var row = item!["stock"]!.AsArray().Single(n => string.Equals(
                    n!["warehouseId"]!.GetValue<string>(), warehouseId.ToString(), StringComparison.OrdinalIgnoreCase));
                return row!["qty"]!.GetValue<decimal>();
            }

            if (items.Count == 0)
            {
                throw new InvalidOperationException($"Item {itemId} not found in any page of the item list.");
            }
        }
    }

    private static decimal Sum(JsonArray gl, string accountCode, string side) =>
        gl.Where(n => string.Equals(
                n!["accountCode"]!.GetValue<string>(), accountCode, StringComparison.Ordinal))
            .Sum(n => n![side]!.GetValue<decimal>());

    private static void AssertTransferGl(JsonNode body, decimal debit1320, decimal credit1310)
    {
        var gl = body["glEntries"]!.AsArray();
        Assert.Equal(debit1320, Sum(gl, "1320", "debit"));
        Assert.Equal(credit1310, Sum(gl, "1310", "credit"));
        Assert.Equal(
            body["totalDebit"]!.GetValue<decimal>(),
            body["totalCredit"]!.GetValue<decimal>());
    }

    // ----------------------------------------------------------------------------- SQL oracles

    private static async Task<int> CountSLEAsync()
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.StockLedgerEntry;", connection);
        return checked((int)(long)(await command.ExecuteScalarAsync())!);
    }

    private static async Task<int> CountGLEntryAsync()
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.GLEntry;", connection);
        return checked((int)(long)(await command.ExecuteScalarAsync())!);
    }

    /// <summary>Net (debit - credit) of one account across the given voucher ids.</summary>
    private static async Task<decimal> ReadAccountNetAsync(IEnumerable<Guid> voucherIds, Guid accountId)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT ISNULL(SUM(Debit), 0) - ISNULL(SUM(Credit), 0) FROM dbo.GLEntry "
            + "WHERE VoucherId IN (SELECT value FROM OPENJSON(@Ids)) AND AccountId = @AccountId;",
            connection);
        command.Parameters.AddWithValue(
            "@Ids", System.Text.Json.JsonSerializer.Serialize(voucherIds.Select(id => id.ToString())));
        command.Parameters.AddWithValue("@AccountId", accountId);
        return (decimal)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>Most recent StockEntry id that is not the given one (the reversal header).</summary>
    private static async Task<Guid> ReadNewEntryIdAsync(Guid knownId)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT TOP 1 Id FROM dbo.StockEntry WHERE Id <> @KnownId ORDER BY CreatedAt DESC;", connection);
        command.Parameters.AddWithValue("@KnownId", knownId);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<JsonNode> ReadEntryAsync(Guid entryId)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT EntryType, WarehouseId, TargetWarehouseId FROM dbo.StockEntry WHERE Id = @Id;", connection);
        command.Parameters.AddWithValue("@Id", entryId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new JsonObject
        {
            ["entryType"] = reader.GetString(0),
            ["warehouseId"] = reader.GetGuid(1).ToString(),
            ["targetWarehouseId"] = await reader.IsDBNullAsync(2) ? null : reader.GetGuid(2).ToString(),
        };
    }

    private static async Task<bool> BomVisibleThroughHistoryAsync(Guid bomId)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT COUNT_BIG(*) FROM dbo.BillOfMaterials FOR SYSTEM_TIME ALL WHERE Id = @Id;", connection);
        command.Parameters.AddWithValue("@Id", bomId);
        return (long)(await command.ExecuteScalarAsync())! >= 1;
    }

    /// <summary>
    /// Direct SQL setup for BOM masters (no BOM-CRUD endpoints exist - out of scope): one
    /// header, its component lines and its workstation operations, with deterministic
    /// per-test GUIDs.
    /// </summary>
    private static async Task InsertBomAsync(
        Guid bomId,
        string bomNumber,
        Guid finishedItemId,
        decimal quantity,
        IEnumerable<(Guid Id, Guid ItemId, decimal Qty, decimal Rate, decimal Amount)> lines,
        IEnumerable<(Guid Id, Guid WorkstationId, string Description, decimal DurationMinutes)> operations)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var bom = new SqlCommand(
            "INSERT INTO dbo.BillOfMaterials (Id, TenantId, CompanyId, BomNumber, ItemId, Quantity, UomId, "
            + "IsActive, IsDefault, RawMaterialCost, OperatingCost, ScrapCost, TotalCost, CreatedAt) "
            + "VALUES (@Id, @TenantId, @CompanyId, @BomNumber, @ItemId, @Quantity, @UomId, 1, 1, 0, 0, 0, 0, SYSDATETIMEOFFSET());",
            connection);
        bom.Parameters.AddWithValue("@Id", bomId);
        bom.Parameters.AddWithValue("@TenantId", ErpApiFactory.DevTenantId);
        bom.Parameters.AddWithValue("@CompanyId", ErpApiFactory.DevCompanyId);
        bom.Parameters.AddWithValue("@BomNumber", bomNumber);
        bom.Parameters.AddWithValue("@ItemId", finishedItemId);
        bom.Parameters.AddWithValue("@Quantity", quantity);
        bom.Parameters.AddWithValue("@UomId", EachUomId);
        await bom.ExecuteNonQueryAsync();

        foreach (var (id, itemId, qty, rate, amount) in lines)
        {
            await using var line = new SqlCommand(
                "INSERT INTO dbo.BomItem (Id, BomId, ItemId, Quantity, UomId, ValuationRate, Amount, ScrapPercentage) "
                + "VALUES (@Id, @BomId, @ItemId, @Quantity, @UomId, @Rate, @Amount, 0);",
                connection);
            line.Parameters.AddWithValue("@Id", id);
            line.Parameters.AddWithValue("@BomId", bomId);
            line.Parameters.AddWithValue("@ItemId", itemId);
            line.Parameters.AddWithValue("@Quantity", qty);
            line.Parameters.AddWithValue("@UomId", EachUomId);
            line.Parameters.AddWithValue("@Rate", rate);
            line.Parameters.AddWithValue("@Amount", amount);
            await line.ExecuteNonQueryAsync();
        }

        foreach (var (id, workstationId, description, duration) in operations)
        {
            await using var operation = new SqlCommand(
                "INSERT INTO dbo.BomOperation (Id, BomId, WorkstationId, Description, DurationMinutes) "
                + "VALUES (@Id, @BomId, @WorkstationId, @Description, @Duration);",
                connection);
            operation.Parameters.AddWithValue("@Id", id);
            operation.Parameters.AddWithValue("@BomId", bomId);
            operation.Parameters.AddWithValue("@WorkstationId", workstationId);
            operation.Parameters.AddWithValue("@Description", description);
            operation.Parameters.AddWithValue("@Duration", duration);
            await operation.ExecuteNonQueryAsync();
        }
    }
}
