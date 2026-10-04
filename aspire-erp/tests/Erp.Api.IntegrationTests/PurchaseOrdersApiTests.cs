using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Erp.Api.IntegrationTests;

/// <summary>
/// Regression for the purchase-order voucher generator: <c>POST /api/v1/purchaseorders</c> used to
/// answer 500 on EVERY call because <c>PurchaseRepository.NextVoucherNumberAsync</c> ran
/// <c>SELECT MAX(VoucherNo) FROM dbo.PurchaseOrder</c> while that table stores its gapless
/// sequence in <c>OrderNumber</c> (SQL error 207, invalid column name). The column is now
/// parameterized exactly like <c>SalesRepository.OrderColumn</c>, so creation must answer 201
/// with sequential <c>PO-YYYY-NNNNN</c> numbers. It also carries the Task 4.2 workflow evidence:
/// the Draft-only PUT line rewrite and the 409 it must answer once the order is Submitted - the
/// endpoint whose handler had no DI registration (Program.cs registers every handler explicitly,
/// there is no assembly scanning), so PUT used to fail before reaching the domain at all.
/// </summary>
/// <remarks>
/// The defect went unnoticed because no integration test ever created an order: unit tests run on
/// fakes and the Task 4.6 chain bills receipts without an order (<c>PurchaseOrderId</c> is
/// optional by contract). An order never posts to the General Ledger by itself (accrual happens on
/// receipt), so this class appends no ledger rows - it still shares
/// <see cref="LedgerMutatingCollection"/> so it cannot interleave with the classes asserting
/// global state. Voucher numbers are read back, never hard-coded: documents are never cleaned up
/// and re-runs only ever add rows.
/// </remarks>
[Collection(LedgerMutatingCollection.Name)]
public class PurchaseOrdersApiTests : IClassFixture<ErpApiFactory>
{
    /// <summary>Demo supplier SUP-001 (seed-dev-buying.sql).</summary>
    private static readonly Guid SupplierId =
        Guid.Parse("e0000000-0000-4000-8000-000000000001");

    /// <summary>Item IT-001 "Steel Bracket" (seed-dev-stock.sql).</summary>
    private static readonly Guid ItemId =
        Guid.Parse("c0000000-0000-4000-8000-000000000001");

    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    private readonly ErpApiFactory _factory;

    public PurchaseOrdersApiTests(ErpApiFactory factory) => _factory = factory;

    /// <summary>
    /// Two consecutive creations both answer 201, and the second number is exactly the first plus
    /// one: the gapless generator survives contact with <c>dbo.PurchaseOrder</c> (the defect this
    /// file regresses) and the range lock keeps the sequence monotonic per company/year.
    /// </summary>
    [Fact]
    public async Task Create_TwoConsecutiveOrders_ReturnsSequentialGaplessOrderNumbers()
    {
        using var client = CreateClient();

        var first = await CreateOrderAsync(client);
        var firstNumber = first["orderNumber"]!.GetValue<string>();

        // Draft is the birth state of the workflow (CreatePurchaseOrderCommand, Task 4.1), and the
        // seeded line must ride along - the aggregate write path, not only the header.
        Assert.Equal("Draft", first["status"]!.GetValue<string>());
        Assert.Matches(@"^PO-\d{4}-\d{5}$", firstNumber);
        Assert.Single(first["items"]!.AsArray());
        Assert.Equal(ItemId, first["items"]![0]!["itemId"]!.GetValue<Guid>());

        var second = await CreateOrderAsync(client);
        var secondNumber = second["orderNumber"]!.GetValue<string>();

        Assert.Matches(@"^PO-\d{4}-\d{5}$", secondNumber);

        // Same company/year prefix on both; fixed 5-digit zero padding means the sequence is the
        // last block: monotonic, gapless, no reuse of the number the previous call just took.
        Assert.Equal(firstNumber[..^5], secondNumber[..^5]);
        Assert.Equal(int.Parse(firstNumber[^5..]) + 1, int.Parse(secondNumber[^5..]));
    }

    /// <summary>
    /// A Draft order accepts a full line rewrite (Task 4.2): header totals are recalculated from
    /// the new lines while the order number and the Draft birth state stay untouched. The 200
    /// itself is the proof that <c>UpdatePurchaseOrderCommandHandler</c> resolves from DI.
    /// </summary>
    [Fact]
    public async Task Update_DraftOrder_RewritesLinesAndRecalculatesTotals()
    {
        using var client = CreateClient();
        var created = await CreateOrderAsync(client); // 10 x 5 = 50
        var orderId = created["id"]!.GetValue<Guid>();

        using var response = await PutOrderAsync(client, orderId, quantity: 7m, rate: 8m);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Order PUT {(int)response.StatusCode}: {body}");

        var updated = (JsonObject)JsonNode.Parse(body)!;
        Assert.Equal(created["orderNumber"]!.GetValue<string>(), updated["orderNumber"]!.GetValue<string>());
        Assert.Equal("Draft", updated["status"]!.GetValue<string>());

        Assert.Equal(56m, updated["netTotal"]!.GetValue<decimal>());
        Assert.Equal(56m, updated["grandTotal"]!.GetValue<decimal>());

        var line = Assert.Single(updated["items"]!.AsArray())!;
        Assert.Equal(7m, line["quantity"]!.GetValue<decimal>());
        Assert.Equal(8m, line["rate"]!.GetValue<decimal>());
        Assert.Equal(56m, line["amount"]!.GetValue<decimal>());
    }

    /// <summary>
    /// Task 4.2 acceptance: line items cannot be altered once submitted. After the
    /// Draft -&gt; Submitted transition the very same PUT is a 409 (<c>invalid_status_transition</c>)
    /// and the stored lines remain the ones the submit froze.
    /// </summary>
    [Fact]
    public async Task Update_AfterSubmit_Returns409InvalidStatusTransition()
    {
        using var client = CreateClient();
        var created = await CreateOrderAsync(client);
        var orderId = created["id"]!.GetValue<Guid>();

        using var submit = await client.PostAsync(
            $"/api/v1/purchaseorders/{orderId}/submit?companyId={ErpApiFactory.DevCompanyId}",
            new StringContent(string.Empty, Encoding.UTF8, "application/json"));

        var submitBody = await submit.Content.ReadAsStringAsync();
        Assert.True(
            submit.StatusCode == HttpStatusCode.OK,
            $"Order submit {(int)submit.StatusCode}: {submitBody}");
        Assert.Equal("Submitted", JsonNode.Parse(submitBody)!["status"]!.GetValue<string>());

        using var response = await PutOrderAsync(client, orderId, quantity: 99m, rate: 1m);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = (JsonObject)JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal(409, problem["status"]!.GetValue<int>());
        Assert.Equal("Conflict", problem["title"]!.GetValue<string>());
        Assert.Equal("invalid_status_transition", problem["code"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(problem["detail"]?.GetValue<string>()));
    }

    // ------------------------------------------------------------------------------- helpers

    /// <summary>Tenant-scoped client (X-Tenant-ID only - see ErpApiFactory remarks on auth).</summary>
    private HttpClient CreateClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-ID", ErpApiFactory.DevTenantId.ToString());
        return client;
    }

    /// <summary>
    /// Posts one order and asserts 201, returning the payload; a non-201 prints the raw body so a
    /// contract drift is diagnosable from the failure message alone.
    /// </summary>
    private async Task<JsonObject> CreateOrderAsync(HttpClient client)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var payload = SerializePayload(new
        {
            companyId = ErpApiFactory.DevCompanyId,
            supplierId = SupplierId,
            transactionDate = Format(today),
            scheduleDate = Format(today.AddDays(7)),
            items = new[] { new { itemId = ItemId, quantity = 10m, rate = 5m } },
        });

        using var response = await client.PostAsync(
            "/api/v1/purchaseorders",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Order POST {(int)response.StatusCode}: {body}");

        return (JsonObject)JsonNode.Parse(body)!;
    }

    /// <summary>
    /// PUTs one Draft rewrite for the given order: the route id and the companyId query parameter
    /// must match the body, otherwise the endpoint answers 400 before the handler runs.
    /// </summary>
    private Task<HttpResponseMessage> PutOrderAsync(
        HttpClient client,
        Guid orderId,
        decimal quantity,
        decimal rate)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var payload = SerializePayload(new
        {
            companyId = ErpApiFactory.DevCompanyId,
            purchaseOrderId = orderId,
            supplierId = SupplierId,
            transactionDate = Format(today),
            scheduleDate = Format(today.AddDays(14)),
            items = new[] { new { itemId = ItemId, quantity, rate } },
        });

        return client.PutAsync(
            $"/api/v1/purchaseorders/{orderId}?companyId={ErpApiFactory.DevCompanyId}",
            new StringContent(payload, Encoding.UTF8, "application/json"));
    }

    /// <summary>Serializes with the web defaults MVC uses, so the body matches what the API binds.</summary>
    private static string SerializePayload<T>(T payload) =>
        JsonSerializer.Serialize(payload, PayloadOptions);

    private static string Format(DateOnly date) =>
        date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
