using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Erp.Api.IntegrationTests;

/// <summary>
/// Tasks 5.2 / 5.2b (Amendment A1) acceptance, end to end over HTTP: the sales order lifecycle
/// (Draft with the gapless SO-YYYY-NNNNN number -&gt; Submitted through the spec SL-02 credit gate)
/// and the delivery note that ships it - FIFO relief, balanced Dr 5210 Cost of Goods Sold / Cr 1310
/// warehouse stock, the gapless DN voucher and the ATOMIC fulfillment update that walks
/// PartiallyDelivered -&gt; Completed while leaving BilledPercentage to Task 5.3.
/// </summary>
/// <remarks>
/// <para><b>Why the LedgerMutating collection.</b> A delivery note appends StockLedgerEntry AND
/// GLEntry rows, so this class must never overlap FiscalPeriodLockApiTests's global
/// <c>SELECT COUNT_BIG(*) FROM dbo.GLEntry</c> oracle - the same reason the journal and
/// financial-report classes share that collection.</para>
///
/// <para><b>Stock provisioning is neutral.</b> Every delivery test posts a MaterialReceipt for
/// EXACTLY the quantity it later ships, so the item's on-hand quantity lands back on its baseline
/// when the test ends (receipt +q and note -q cancel out in the Kardex). The ledger rows themselves
/// stay: Constitution Article III.2 makes the ledger append-only, so only the SELLING documents are
/// cleaned up below.</para>
///
/// <para><b>Why the customer code prefix is <c>SOIT-</c> and not <c>IT-</c>.</b>
/// CustomersApiTests runs CONCURRENTLY with this class (it deliberately stays out of
/// LedgerMutating) and its Dispose deletes every <c>IT-%</c> customer of the dev tenant. An
/// <c>IT-</c> customer of mine could be deleted while one of my orders still references it, and
/// that class would then fail its own teardown on the foreign key. Different prefix, no overlap.</para>
///
/// <para><b>Cleanup.</b> Dispose deletes the delivery notes, sales orders and <c>SOIT-</c>
/// customers this class created so re-runs start from a clean slate; GLEntry / StockLedgerEntry
/// rows remain, exactly like the known side effect FinancialReportsApiTests documents.</para>
/// </remarks>
[Collection(LedgerMutatingCollection.Name)]
public class SalesOrderDeliveryNoteApiTests : IClassFixture<ErpApiFactory>, IDisposable
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";
    private const string CustomersPath = "/api/v1/customers";
    private const string SalesOrdersPath = "/api/v1/sales-orders";
    private const string DeliveryNotesPath = "/api/v1/delivery-notes";
    private const string StockEntriesPath = "/api/v1/stockentries";

    /// <summary>scripts/seed-dev-stock.sql: IT-001 Steel Bracket - the only seeded FIFO item this class needs.</summary>
    private static readonly Guid ItemId = Guid.Parse("c0000000-0000-4000-8000-000000000001");

    /// <summary>scripts/seed-dev-stock.sql: WH-01 Main Stores - posts its inventory value to 1310 Stock In Hand.</summary>
    private static readonly Guid WarehouseId = Guid.Parse("d0000000-0000-4000-8000-000000000002");

    /// <summary>
    /// Reference date of this run (the posting date of every document). Stamped ONCE so the SO/DN
    /// year used in the number assertions cannot drift from the dates actually sent.
    /// </summary>
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly ErpApiFactory _factory;

    public SalesOrderDeliveryNoteApiTests(ErpApiFactory factory) => _factory = factory;

    // ----------------------------------------------------------------- Task 5.2: order lifecycle

    /// <summary>
    /// Task 5.2 (creation half): 201 Created with a Location, the gapless SO-YYYY-NNNNN number on
    /// both orders of the run with a STRICTLY increasing sequence, Draft status and the
    /// server-computed totals (the client never sends money).
    /// </summary>
    [Fact]
    public async Task PostSalesOrder_Returns201WithGaplessOrderNumberThatStrictlyIncreases()
    {
        using var client = CreateClient(ErpApiFactory.DevTenantId);
        var customerId = await CreateCustomerAsync(client);

        var first = await CreateOrderAsync(client, customerId, quantity: 10m, rate: 25.5m);
        var second = await CreateOrderAsync(client, customerId, quantity: 2m, rate: 3m);

        var firstNumber = first["orderNumber"]!.GetValue<string>();
        var secondNumber = second["orderNumber"]!.GetValue<string>();

        Assert.Matches($@"^SO-{Today.Year}-\d{{5}}$", firstNumber);
        Assert.Matches($@"^SO-{Today.Year}-\d{{5}}$", secondNumber);
        Assert.True(
            SequenceOf(secondNumber) > SequenceOf(firstNumber),
            $"The gapless generator must strictly increase within a year ({firstNumber} -> {secondNumber}).");

        // Draft on creation: no stock and no GL impact, counters still at zero.
        Assert.Equal("Draft", first["status"]!.GetValue<string>());
        Assert.Equal(0m, first["deliveredPercentage"]!.GetValue<decimal>());
        Assert.Equal(0m, first["billedPercentage"]!.GetValue<decimal>());

        // Server-computed money: Round4(10 * 25.5000) = 255.0000, no tax engine yet (Task 5.3).
        Assert.Equal(255m, first["netTotal"]!.GetValue<decimal>());
        Assert.Equal(0m, first["taxTotal"]!.GetValue<decimal>());
        Assert.Equal(255m, first["grandTotal"]!.GetValue<decimal>());

        var line = first["lines"]!.AsArray()[0]!;
        Assert.Equal(10m, line["quantity"]!.GetValue<decimal>());
        Assert.Equal(25.5m, line["rate"]!.GetValue<decimal>());
        Assert.Equal(255m, line["amount"]!.GetValue<decimal>());
        Assert.Equal(0m, line["deliveredQuantity"]!.GetValue<decimal>());
        Assert.Equal(0m, line["billedQuantity"]!.GetValue<decimal>());
    }

    /// <summary>
    /// Task 5.2 (read side): the created order round-trips through GET by id (header + line +
    /// customer identity), shows up in the company-scoped list, and is INVISIBLE to a foreign tenant
    /// looking at the same companyId (Constitution Article II.3).
    /// </summary>
    [Fact]
    public async Task GetSalesOrderEndpoints_ReturnTheCreatedOrderByIdAndInCompanyList()
    {
        using var client = CreateClient(ErpApiFactory.DevTenantId);
        var customerId = await CreateCustomerAsync(client);
        var created = await CreateOrderAsync(client, customerId);
        var id = created["id"]!.GetValue<Guid>();

        using var byId = await client.GetAsync($"{SalesOrdersPath}/{id}?companyId={ErpApiFactory.DevCompanyId}");
        Assert.Equal(HttpStatusCode.OK, byId.StatusCode);

        var fetched = JsonNode.Parse(await byId.Content.ReadAsStringAsync())!;
        Assert.Equal(id, fetched["id"]!.GetValue<Guid>());
        Assert.Equal(created["orderNumber"]!.GetValue<string>(), fetched["orderNumber"]!.GetValue<string>());
        Assert.Equal(created["customerCode"]!.GetValue<string>(), fetched["customerCode"]!.GetValue<string>());
        Assert.Single(fetched["lines"]!.AsArray());

        using var list = await client.GetAsync($"{SalesOrdersPath}?companyId={ErpApiFactory.DevCompanyId}");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var orders = JsonNode.Parse(await list.Content.ReadAsStringAsync())!["items"]!.AsArray();
        Assert.Contains(orders, node => node!["id"]!.GetValue<Guid>() == id);

        using var foreignClient = CreateClient(ErpApiFactory.ForeignTenantId);
        using var foreign = await foreignClient.GetAsync(
            $"{SalesOrdersPath}?companyId={ErpApiFactory.DevCompanyId}");
        Assert.Equal(HttpStatusCode.OK, foreign.StatusCode);
        Assert.Empty(JsonNode.Parse(await foreign.Content.ReadAsStringAsync())!["items"]!.AsArray());
    }

    /// <summary>
    /// Task 5.2 (workflow): Draft -&gt; Submitted returns 200 with the new status, and the transition
    /// is PERSISTED (a fresh GET sees Submitted, not just the response body).
    /// </summary>
    [Fact]
    public async Task SubmitSalesOrder_FromDraft_Returns200WithSubmittedStatus()
    {
        using var client = CreateClient(ErpApiFactory.DevTenantId);
        var customerId = await CreateCustomerAsync(client);
        var order = await CreateOrderAsync(client, customerId);
        var id = order["id"]!.GetValue<Guid>();

        using var response = await SubmitAsync(client, id);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var submitted = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal("Submitted", submitted["status"]!.GetValue<string>());
        Assert.Equal(order["orderNumber"]!.GetValue<string>(), submitted["orderNumber"]!.GetValue<string>());

        using var byId = await client.GetAsync($"{SalesOrdersPath}/{id}?companyId={ErpApiFactory.DevCompanyId}");
        Assert.Equal(HttpStatusCode.OK, byId.StatusCode);
        Assert.Equal("Submitted", JsonNode.Parse(await byId.Content.ReadAsStringAsync())!["status"]!.GetValue<string>());
    }

    /// <summary>
    /// spec SL-02 / plan.md §2 credit gate: an exposure above the customer's limit is a 409
    /// <c>credit_limit_exceeded</c> AND the order stays in Draft - a rejected submit writes nothing.
    /// </summary>
    [Fact]
    public async Task SubmitSalesOrder_WhenCreditLimitBreached_Returns409AndOrderStaysDraft()
    {
        using var client = CreateClient(ErpApiFactory.DevTenantId);

        // The limit must be POSITIVE (CreditLimit &lt;= 0 switches credit control off entirely) yet
        // far below the 255.0000 order value, so the breach is unambiguous.
        var customerId = await CreateCustomerAsync(client, creditLimit: 1m);
        var order = await CreateOrderAsync(client, customerId, quantity: 10m, rate: 25.5m);
        var id = order["id"]!.GetValue<Guid>();

        using var response = await SubmitAsync(client, id);
        await AssertProblemAsync(
            response,
            HttpStatusCode.Conflict,
            "Credit Limit Exceeded",
            "credit_limit_exceeded");

        using var byId = await client.GetAsync($"{SalesOrdersPath}/{id}?companyId={ErpApiFactory.DevCompanyId}");
        Assert.Equal(HttpStatusCode.OK, byId.StatusCode);

        var unchanged = JsonNode.Parse(await byId.Content.ReadAsStringAsync())!;
        Assert.Equal("Draft", unchanged["status"]!.GetValue<string>());
        Assert.Equal(0m, unchanged["deliveredPercentage"]!.GetValue<decimal>());
    }

    /// <summary>
    /// Task 5.2 workflow guard: only Draft is submittable, so a second POST /submit is a 409 with
    /// <c>invalid_status_transition</c> instead of a silent no-op.
    /// </summary>
    [Fact]
    public async Task SubmitSalesOrder_WhenAlreadySubmitted_Returns409WithInvalidStatusTransition()
    {
        using var client = CreateClient(ErpApiFactory.DevTenantId);
        var customerId = await CreateCustomerAsync(client);
        var order = await CreateOrderAsync(client, customerId);
        var id = order["id"]!.GetValue<Guid>();

        using (var first = await SubmitAsync(client, id))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        using var second = await SubmitAsync(client, id);
        await AssertProblemAsync(
            second,
            HttpStatusCode.Conflict,
            "Sales Order Conflict",
            "invalid_status_transition");
    }

    // ------------------------------------------------- Task 5.2b: delivery note posting & SL-04

    /// <summary>
    /// Task 5.2b acceptance (full shipment): 201 Created with the gapless DN voucher, a BALANCED
    /// Dr 5210 / Cr 1310 pair (asserted by account code and by equality - never by absolute amount,
    /// the shared FIFO layers accumulate across runs), negative Kardex rows that book NO StockEntry,
    /// the customer stamped as the GL party, and the order landing on Completed / 100% delivered /
    /// 0% billed. The note also round-trips through both GET endpoints.
    /// </summary>
    [Fact]
    public async Task PostDeliveryNote_FullQuantity_Returns201BalancedGlAndCompletesOrder()
    {
        using var client = CreateClient(ErpApiFactory.DevTenantId);
        var customerId = await CreateCustomerAsync(client);
        var order = await CreateOrderAsync(client, customerId, quantity: 10m, rate: 25.5m);
        var orderId = order["id"]!.GetValue<Guid>();
        var orderLineId = order["lines"]!.AsArray()[0]!["id"]!.GetValue<Guid>();

        using (var submitted = await SubmitAsync(client, orderId))
        {
            Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        }

        // Exactly the quantity that will ship, so the Kardex ends where it started (see class remarks).
        await ReceiveStockAsync(client, quantity: 10m);

        using var response = await PostDeliveryNoteAsync(client, orderId, orderLineId, quantity: 10m);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var posting = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var note = posting["deliveryNote"]!;
        var voucherNo = note["voucherNo"]!.GetValue<string>();
        Assert.Matches($@"^DN-{Today.Year}-\d{{5}}$", voucherNo);
        Assert.Equal(orderId, note["salesOrderId"]!.GetValue<Guid>());
        Assert.Equal(WarehouseId, note["warehouseId"]!.GetValue<Guid>());

        // tasks.md 5.2b / spec SL-01: Debit Company.CogsAccountCode (5210) / Credit the warehouse
        // stock account (1310) - one balanced pair per delivered line.
        var gl = posting["glEntries"]!.AsArray();
        Assert.Equal(2, gl.Count);

        JsonNode debit = gl.Single(g => g!["accountCode"]!.GetValue<string>() == "5210")!;
        JsonNode credit = gl.Single(g => g!["accountCode"]!.GetValue<string>() == "1310")!;
        Assert.True(debit["debit"]!.GetValue<decimal>() > 0m, "The COGS line must carry a debit.");
        Assert.Equal(0m, debit["credit"]!.GetValue<decimal>());
        Assert.True(credit["credit"]!.GetValue<decimal>() > 0m, "The stock line must carry a credit.");
        Assert.Equal(0m, credit["debit"]!.GetValue<decimal>());
        Assert.Equal(credit["credit"]!.GetValue<decimal>(), debit["debit"]!.GetValue<decimal>());

        Assert.All(gl, g => Assert.Equal("DeliveryNote", g!["voucherType"]!.GetValue<string>()));
        Assert.All(gl, g => Assert.Equal(voucherNo, g!["voucherNo"]!.GetValue<string>()));
        Assert.Equal(
            posting["totalDebit"]!.GetValue<decimal>(),
            posting["totalCredit"]!.GetValue<decimal>());

        // -Kardex: one NEGATIVE QtyChange per line - stock is relieved at FIFO cost, and the
        // delivery note never creates a StockEntry row of its own (StockEntryId stays NULL).
        var ledger = posting["ledgerEntries"]!.AsArray();
        Assert.Single(ledger);
        Assert.True(ledger[0]!["qtyChange"]!.GetValue<decimal>() < 0m, "A delivery must relieve stock.");
        Assert.Equal(ItemId, ledger[0]!["itemId"]!.GetValue<Guid>());
        Assert.Equal(WarehouseId, ledger[0]!["warehouseId"]!.GetValue<Guid>());

        var (ledgerVoucherType, stockEntryId) = await ReadLedgerProvenanceAsync(voucherNo);
        Assert.Equal("DeliveryNote", ledgerVoucherType);
        Assert.Null(stockEntryId);

        // plan.md §2: the customer is the counterparty of every GL line of the note (VoucherId is
        // the unique DN id, so this reads THIS run's rows even after a previous run left its own).
        await AssertGlPartyAsync(note["id"]!.GetValue<Guid>(), customerId);

        // Task 5.2b: the order advanced inside the SAME transaction as the ledger.
        var salesOrder = posting["salesOrder"]!;
        Assert.Equal("Completed", salesOrder["status"]!.GetValue<string>());
        Assert.Equal(100m, salesOrder["deliveredPercentage"]!.GetValue<decimal>());
        Assert.Equal(0m, salesOrder["billedPercentage"]!.GetValue<decimal>()); // Task 5.3 owns it

        var shippedLine = salesOrder["lines"]!.AsArray()[0]!;
        Assert.Equal(10m, shippedLine["deliveredQuantity"]!.GetValue<decimal>());
        Assert.Equal(100m, shippedLine["deliveredPercentage"]!.GetValue<decimal>());
        Assert.Equal(0m, shippedLine["billedQuantity"]!.GetValue<decimal>());

        // The note is readable back through both read endpoints.
        using var byId = await client.GetAsync(
            $"{DeliveryNotesPath}/{note["id"]!.GetValue<Guid>()}?companyId={ErpApiFactory.DevCompanyId}");
        Assert.Equal(HttpStatusCode.OK, byId.StatusCode);

        var fetched = JsonNode.Parse(await byId.Content.ReadAsStringAsync())!;
        Assert.Equal(voucherNo, fetched["voucherNo"]!.GetValue<string>());
        Assert.Equal(orderId, fetched["salesOrderId"]!.GetValue<Guid>());
        Assert.Single(fetched["lines"]!.AsArray());

        using var list = await client.GetAsync($"{DeliveryNotesPath}?companyId={ErpApiFactory.DevCompanyId}");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var notes = JsonNode.Parse(await list.Content.ReadAsStringAsync())!["items"]!.AsArray();
        Assert.Contains(notes, node => node!["id"]!.GetValue<Guid>() == note["id"]!.GetValue<Guid>());
    }

    /// <summary>
    /// Task 5.2b acceptance (partial shipment): 4 of 10 moves the order to PartiallyDelivered /
    /// 40%, the remaining 6 move it to Completed / 100%, and BilledPercentage stays at 0 throughout
    /// (billing belongs to Task 5.3).
    /// </summary>
    [Fact]
    public async Task PostDeliveryNote_PartialThenRemainingDelivery_TransitionsPartiallyDeliveredThenCompleted()
    {
        using var client = CreateClient(ErpApiFactory.DevTenantId);
        var customerId = await CreateCustomerAsync(client);
        var order = await CreateOrderAsync(client, customerId, quantity: 10m, rate: 25.5m);
        var orderId = order["id"]!.GetValue<Guid>();
        var orderLineId = order["lines"]!.AsArray()[0]!["id"]!.GetValue<Guid>();

        using (var submitted = await SubmitAsync(client, orderId))
        {
            Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        }

        await ReceiveStockAsync(client, quantity: 10m); // 4 + 6 shipped below

        using (var partial = await PostDeliveryNoteAsync(client, orderId, orderLineId, quantity: 4m))
        {
            Assert.Equal(HttpStatusCode.Created, partial.StatusCode);

            var afterPartial = JsonNode.Parse(await partial.Content.ReadAsStringAsync())!["salesOrder"]!;
            Assert.Equal("PartiallyDelivered", afterPartial["status"]!.GetValue<string>());
            Assert.Equal(40m, afterPartial["deliveredPercentage"]!.GetValue<decimal>());
            Assert.Equal(0m, afterPartial["billedPercentage"]!.GetValue<decimal>());
            Assert.Equal(4m, afterPartial["lines"]!.AsArray()[0]!["deliveredQuantity"]!.GetValue<decimal>());
        }

        using var rest = await PostDeliveryNoteAsync(client, orderId, orderLineId, quantity: 6m);
        Assert.Equal(HttpStatusCode.Created, rest.StatusCode);

        var afterRest = JsonNode.Parse(await rest.Content.ReadAsStringAsync())!["salesOrder"]!;
        Assert.Equal("Completed", afterRest["status"]!.GetValue<string>());
        Assert.Equal(100m, afterRest["deliveredPercentage"]!.GetValue<decimal>());
        Assert.Equal(0m, afterRest["billedPercentage"]!.GetValue<decimal>());
        Assert.Equal(10m, afterRest["lines"]!.AsArray()[0]!["deliveredQuantity"]!.GetValue<decimal>());
    }

    /// <summary>
    /// spec SL-04 non-overdelivery guard: the ceiling is <c>Quantity - DeliveredQuantity</c>, so it
    /// rejects BOTH a note above the ordered quantity (11 of 10) and a note above what the order
    /// still owes after a partial delivery (7 of the 6 remaining). Both rejections are 400
    /// <c>overdelivery_not_allowed</c> and write nothing - the counters keep the 4 already shipped.
    /// </summary>
    [Fact]
    public async Task PostDeliveryNote_ExceedingRemainingQuantity_Returns400WithOverdeliveryNotAllowed()
    {
        using var client = CreateClient(ErpApiFactory.DevTenantId);
        var customerId = await CreateCustomerAsync(client);
        var order = await CreateOrderAsync(client, customerId, quantity: 10m, rate: 25.5m);
        var orderId = order["id"]!.GetValue<Guid>();
        var orderLineId = order["lines"]!.AsArray()[0]!["id"]!.GetValue<Guid>();

        using (var submitted = await SubmitAsync(client, orderId))
        {
            Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        }

        await ReceiveStockAsync(client, quantity: 4m); // only the 4 that really ship below

        // (a) Above the ORDERED quantity while nothing was delivered yet.
        using (var tooMuch = await PostDeliveryNoteAsync(client, orderId, orderLineId, quantity: 11m))
        {
            await AssertProblemAsync(
                tooMuch,
                HttpStatusCode.BadRequest,
                "Delivery Note Rejected",
                "overdelivery_not_allowed");
        }

        // Ship a legal partial quantity so the ceiling drops to 10 - 4 = 6.
        using (var partial = await PostDeliveryNoteAsync(client, orderId, orderLineId, quantity: 4m))
        {
            Assert.Equal(HttpStatusCode.Created, partial.StatusCode);
        }

        // (b) Above what the order line STILL OWES - the literal SL-04 expression.
        using (var overTheRest = await PostDeliveryNoteAsync(client, orderId, orderLineId, quantity: 7m))
        {
            await AssertProblemAsync(
                overTheRest,
                HttpStatusCode.BadRequest,
                "Delivery Note Rejected",
                "overdelivery_not_allowed");
        }

        // Neither rejected attempt wrote anything: one note exists and it carries only the 4 units.
        using var byId = await client.GetAsync($"{SalesOrdersPath}/{orderId}?companyId={ErpApiFactory.DevCompanyId}");
        Assert.Equal(HttpStatusCode.OK, byId.StatusCode);

        var unchanged = JsonNode.Parse(await byId.Content.ReadAsStringAsync())!;
        Assert.Equal("PartiallyDelivered", unchanged["status"]!.GetValue<string>());
        Assert.Equal(40m, unchanged["deliveredPercentage"]!.GetValue<decimal>());
        Assert.Equal(4m, unchanged["lines"]!.AsArray()[0]!["deliveredQuantity"]!.GetValue<decimal>());

        using var list = await client.GetAsync($"{DeliveryNotesPath}?companyId={ErpApiFactory.DevCompanyId}");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var notes = JsonNode.Parse(await list.Content.ReadAsStringAsync())!["items"]!.AsArray();
        Assert.Equal(1, notes.Count(node => node!["salesOrderId"]!.GetValue<Guid>() == orderId));
    }

    /// <summary>
    /// Task 5.2b workflow guard: a Draft order has not passed the credit gate, so a delivery note
    /// against it is a 409 <c>sales_order_not_deliverable</c> and the order stays in Draft.
    /// </summary>
    [Fact]
    public async Task PostDeliveryNote_ForDraftOrder_Returns409WithSalesOrderNotDeliverable()
    {
        using var client = CreateClient(ErpApiFactory.DevTenantId);
        var customerId = await CreateCustomerAsync(client);
        var order = await CreateOrderAsync(client, customerId);
        var orderId = order["id"]!.GetValue<Guid>();
        var orderLineId = order["lines"]!.AsArray()[0]!["id"]!.GetValue<Guid>();

        using var response = await PostDeliveryNoteAsync(client, orderId, orderLineId, quantity: 1m);
        await AssertProblemAsync(
            response,
            HttpStatusCode.Conflict,
            "Sales Order Conflict",
            "sales_order_not_deliverable");

        using var byId = await client.GetAsync($"{SalesOrdersPath}/{orderId}?companyId={ErpApiFactory.DevCompanyId}");
        Assert.Equal(HttpStatusCode.OK, byId.StatusCode);
        Assert.Equal("Draft", JsonNode.Parse(await byId.Content.ReadAsStringAsync())!["status"]!.GetValue<string>());
    }

    // --------------------------------------------------------------------------- read-edge cases

    /// <summary>
    /// Unknown ids are 404s with the stable snake_case code for BOTH documents - the read side of
    /// the API never leaks whether the row exists in another tenant/company.
    /// </summary>
    [Fact]
    public async Task GetDocumentById_WithUnknownId_Returns404WithStableCodes()
    {
        using var client = CreateClient(ErpApiFactory.DevTenantId);
        var unknown = Guid.NewGuid();

        using var order = await client.GetAsync($"{SalesOrdersPath}/{unknown}?companyId={ErpApiFactory.DevCompanyId}");
        await AssertProblemAsync(order, HttpStatusCode.NotFound, "Sales Order Not Found", "sales_order_not_found");

        using var note = await client.GetAsync($"{DeliveryNotesPath}/{unknown}?companyId={ErpApiFactory.DevCompanyId}");
        await AssertProblemAsync(
            note,
            HttpStatusCode.NotFound,
            "Delivery Note Not Found",
            "delivery_note_not_found");
    }

    /// <summary>
    /// Both list endpoints demand a non-empty <c>companyId</c> (the company IS the scope of a
    /// selling document) and answer with 400 <c>company_required</c> instead of silently listing
    /// nothing.
    /// </summary>
    [Fact]
    public async Task GetDocumentLists_WithoutCompanyId_Return400WithCompanyRequired()
    {
        using var client = CreateClient(ErpApiFactory.DevTenantId);

        using var orders = await client.GetAsync(SalesOrdersPath);
        await AssertProblemAsync(orders, HttpStatusCode.BadRequest, "Invalid Company", "company_required");

        using var notes = await client.GetAsync(DeliveryNotesPath);
        await AssertProblemAsync(notes, HttpStatusCode.BadRequest, "Invalid Company", "company_required");
    }

    /// <summary>
    /// Constitution Article VI.4: a delivery note writes StockLedgerEntry AND GLEntry, so the POST
    /// REQUIRES an <c>Idempotency-Key</c> header - a missing one is rejected by the guard BEFORE the
    /// handler runs (no note, no order change).
    /// </summary>
    [Fact]
    public async Task PostDeliveryNote_WithoutIdempotencyKey_Returns400WithIdempotencyKeyRequired()
    {
        using var client = CreateClient(ErpApiFactory.DevTenantId);
        var customerId = await CreateCustomerAsync(client);
        var order = await CreateOrderAsync(client, customerId);
        var orderId = order["id"]!.GetValue<Guid>();
        var orderLineId = order["lines"]!.AsArray()[0]!["id"]!.GetValue<Guid>();

        using var response = await PostDeliveryNoteAsync(
            client, orderId, orderLineId, quantity: 1m, withIdempotencyKey: false);

        await AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            "Idempotency Key Required",
            "idempotency_key_required");

        using var byId = await client.GetAsync($"{SalesOrdersPath}/{orderId}?companyId={ErpApiFactory.DevCompanyId}");
        Assert.Equal(HttpStatusCode.OK, byId.StatusCode);
        Assert.Equal("Draft", JsonNode.Parse(await byId.Content.ReadAsStringAsync())!["status"]!.GetValue<string>());
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
    /// Creates one <c>SOIT-</c> customer for this test (the prefix is explained in the class
    /// remarks) and returns its id. <paramref name="creditLimit"/> is 0 = credit control switched
    /// off, which is what every test except the SL-02 breach wants.
    /// </summary>
    private async Task<Guid> CreateCustomerAsync(HttpClient client, decimal creditLimit = 0m)
    {
        var payload = new
        {
            companyId = ErpApiFactory.DevCompanyId,
            customerCode = $"SOIT-{Guid.NewGuid().ToString("N")[..8]}",
            customerName = "Selling Workflow Buyer",
            creditLimit,
            bypassCreditLimitCheck = false,
        };

        using var response = await client.PostAsJsonAsync(CustomersPath, payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        return body["id"]!.GetValue<Guid>();
    }

    /// <summary>POSTs one single-line sales order and asserts the 201 contract, returning the created payload.</summary>
    private async Task<JsonNode> CreateOrderAsync(
        HttpClient client,
        Guid customerId,
        decimal quantity = 10m,
        decimal rate = 25.5m)
    {
        var payload = new
        {
            companyId = ErpApiFactory.DevCompanyId,
            customerId,
            transactionDate = Iso(Today),
            deliveryDate = Iso(Today.AddDays(7)),
            lines = new[] { new { itemId = ItemId, quantity, rate } },
        };

        using var response = await client.PostAsJsonAsync(SalesOrdersPath, payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
    }

    /// <summary>POSTs <c>/submit</c> for one order (fresh request each time - the caller asserts the outcome).</summary>
    private static Task<HttpResponseMessage> SubmitAsync(HttpClient client, Guid orderId) =>
        client.PostAsync($"{SalesOrdersPath}/{orderId}/submit?companyId={ErpApiFactory.DevCompanyId}", null);

    /// <summary>
    /// Posts a MaterialReceipt of <paramref name="quantity"/> IT-001 into WH-01 so the delivery
    /// below has its own FIFO layer. A fresh Idempotency-Key per call (Constitution VI.4).
    /// </summary>
    private async Task ReceiveStockAsync(HttpClient client, decimal quantity)
    {
        var payload = new
        {
            companyId = ErpApiFactory.DevCompanyId,
            entryType = "MaterialReceipt", // JsonStringEnumConverter is registered in Program.cs
            warehouseId = WarehouseId,
            postingDate = Iso(Today),
            lines = new[] { new { itemId = ItemId, qty = quantity, rate = 10m } },
        };

        var request = new HttpRequestMessage(HttpMethod.Post, StockEntriesPath)
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add(IdempotencyKeyHeader, Guid.NewGuid().ToString("N"));

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    /// <summary>
    /// POSTs one delivery note line against the given order line. Every call carries a FRESH
    /// <c>Idempotency-Key</c> unless <paramref name="withIdempotencyKey"/> is false (that switch
    /// exists to prove the Article VI.4 guard rejects the request before anything else runs).
    /// </summary>
    private async Task<HttpResponseMessage> PostDeliveryNoteAsync(
        HttpClient client,
        Guid orderId,
        Guid orderLineId,
        decimal quantity,
        bool withIdempotencyKey = true)
    {
        var payload = new
        {
            companyId = ErpApiFactory.DevCompanyId,
            salesOrderId = orderId,
            warehouseId = WarehouseId,
            postingDate = Iso(Today),
            lines = new[] { new { salesOrderItemId = orderLineId, itemId = ItemId, qty = quantity } },
        };

        var request = new HttpRequestMessage(HttpMethod.Post, DeliveryNotesPath)
        {
            Content = JsonContent.Create(payload),
        };

        if (withIdempotencyKey)
        {
            request.Headers.Add(IdempotencyKeyHeader, Guid.NewGuid().ToString("N"));
        }

        return await client.SendAsync(request);
    }

    /// <summary>
    /// Asserts the RFC 7807 contract of a rejection: status line, <c>status</c> extension, title
    /// and the stable machine code in the <c>code</c> extension (mirrors CustomersApiTests).
    /// </summary>
    private static async Task<JsonNode> AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedTitle,
        string expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);

        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal((int)expectedStatus, problem["status"]!.GetValue<int>());
        Assert.Equal(expectedTitle, problem["title"]!.GetValue<string>());
        Assert.Equal(expectedCode, problem["code"]!.GetValue<string>());
        return problem;
    }

    /// <summary><c>SO-2026-00042</c> -&gt; 42 (the gapless sequence of a document number).</summary>
    private static int SequenceOf(string documentNumber) =>
        int.Parse(documentNumber.Split('-')[2], System.Globalization.CultureInfo.InvariantCulture);

    private static string Iso(DateOnly date) =>
        date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Reads the Kardex provenance of one DN voucher straight from the database: VoucherType must be
    /// <c>DeliveryNote</c> and StockEntryId must be NULL (Task 5.2b books no stock voucher). The
    /// newest row wins so a previous run of this test - which left its immutable ledger rows behind
    /// while its document was cleaned up - cannot be mistaken for this run's row.
    /// </summary>
    private static async Task<(string VoucherType, Guid? StockEntryId)> ReadLedgerProvenanceAsync(
        string voucherNo)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT TOP (1) VoucherType, StockEntryId "
            + "FROM dbo.StockLedgerEntry WITH (NOLOCK) "
            + "WHERE VoucherNo = @VoucherNo ORDER BY CreatedAt DESC;",
            connection);
        command.Parameters.AddWithValue("@VoucherNo", voucherNo);

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(
            await reader.ReadAsync(),
            $"No StockLedgerEntry row was written for DN voucher '{voucherNo}'.");

        return (reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetGuid(1));
    }

    /// <summary>
    /// Asserts that EVERY General Ledger line of one delivery note carries the customer as its
    /// party (plan.md §2 PartyType/PartyId) - the response DTO does not expose those columns, so
    /// the check has to read the ledger.
    /// </summary>
    private static async Task AssertGlPartyAsync(Guid deliveryNoteId, Guid customerId)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT PartyType, PartyId FROM dbo.GLEntry WHERE VoucherId = @VoucherId;",
            connection);
        command.Parameters.AddWithValue("@VoucherId", deliveryNoteId);

        var rows = new List<(string? PartyType, Guid? PartyId)>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                rows.Add((
                    reader.IsDBNull(0) ? null : reader.GetString(0),
                    reader.IsDBNull(1) ? null : reader.GetGuid(1)));
            }
        }

        Assert.Equal(2, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.Equal("Customer", row.PartyType);
            Assert.Equal(customerId, row.PartyId);
        });
    }

    /// <summary>
    /// Removes the selling documents this class created (and the <c>SOIT-</c> customers they
    /// reference) so re-runs stay idempotent. FK order matters: note lines -&gt; notes -&gt; order
    /// lines -&gt; orders -&gt; customers. Ledger rows are deliberately NOT touched (append-only).
    /// </summary>
    public void Dispose()
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            "DECLARE @mine TABLE (Id uniqueidentifier); "
            + "INSERT INTO @mine (Id) "
            + "SELECT Id FROM dbo.Customer "
            + "WHERE TenantId = @TenantId AND CustomerCode LIKE N'SOIT-%'; "
            + "DELETE FROM dbo.DeliveryNoteLine "
            + "WHERE DeliveryNoteId IN (SELECT dn.Id FROM dbo.DeliveryNote dn "
            + "  JOIN dbo.SalesOrder so ON so.Id = dn.SalesOrderId "
            + "  JOIN @mine m ON m.Id = so.CustomerId); "
            + "DELETE FROM dbo.DeliveryNote "
            + "WHERE SalesOrderId IN (SELECT so.Id FROM dbo.SalesOrder so "
            + "  JOIN @mine m ON m.Id = so.CustomerId); "
            + "DELETE FROM dbo.SalesOrderItem "
            + "WHERE SalesOrderId IN (SELECT so.Id FROM dbo.SalesOrder so "
            + "  JOIN @mine m ON m.Id = so.CustomerId); "
            + "DELETE FROM dbo.SalesOrder WHERE CustomerId IN (SELECT Id FROM @mine); "
            + "DELETE FROM dbo.Customer WHERE Id IN (SELECT Id FROM @mine);";
        command.Parameters.AddWithValue("@TenantId", ErpApiFactory.DevTenantId);
        command.ExecuteNonQuery();
    }
}
