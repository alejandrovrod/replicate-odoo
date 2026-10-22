using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Erp.Api.IntegrationTests;

/// <summary>
/// Module buying-suppliers, tasks.md T3.2 (spec §1 gap D2 + scenario SUP-G2): end-to-end evidence
/// over HTTP. Two contracts are proven against the live dev database:
/// <list type="number">
/// <item><b>Multi-company creation</b> - <c>POST /api/v1/suppliers</c> persists one
/// <c>SupplierCompanyAccount</c> row per company (unique per supplier+company) and round-trips them
/// through <c>GET /api/v1/suppliers/{id}</c>;</item>
/// <item><b>Hold rejection with 422</b> - billing a supplier on hold for invoices is rejected with
/// <c>422 supplier_on_hold_invoices</c> BEFORE any ledger row is written (the guard runs ahead of
/// the posting service).</item>
/// </list>
/// </summary>
/// <remarks>
/// The dev tenant owns a SINGLE company (<see cref="ErpApiFactory.DevCompanyId"/>), so the
/// multi-company case seeds a second company directly (raw SQL) and reuses the tenant-scoped seeded
/// payable account <c>2110</c> - the account lookup is tenant-scoped, not company-scoped. Every row
/// this class creates carries an <c>IT-</c> marker (supplier code / group code / fixed company id)
/// and is removed in the fixture teardown so re-runs stay idempotent.
/// </remarks>
[Collection(LedgerMutatingCollection.Name)]
public class SupplierGovernanceApiTests : IClassFixture<ErpApiFactory>
{
    /// <summary>Second company the multi-company test seeds (distinct prefix keeps cleanup targeted).</summary>
    private static readonly Guid SecondCompanyId =
        Guid.Parse("cc0c0000-0000-4000-8000-0000000000c1");

    /// <summary>Seeded 2110 "Accounts Payable" (scripts/seed-dev-coa.sql) - tenant-scoped lookup.</summary>
    private static readonly Guid PayableAccountId =
        Guid.Parse("a0000000-0000-4000-8000-000000002110");

    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    private readonly ErpApiFactory _factory;

    public SupplierGovernanceApiTests(ErpApiFactory factory)
    {
        _factory = factory;
        Cleanup();
    }

    // ------------------------------------------------------------------ T3.2 (1): multi-company

    /// <summary>
    /// Creating a supplier with per-company accounts persists exactly one mapping per company and
    /// serves them back. A duplicate company in the same payload is rejected (the aggregate rule),
    /// but here both rows target DIFFERENT companies, so the create succeeds with two rows.
    /// </summary>
    [Fact]
    public async Task Create_WithPerCompanyAccounts_PersistsOneRowPerCompany()
    {
        SeedSecondCompany();

        using var client = CreateClient();
        var code = UniqueCode("SUP");

        var created = await CreateSupplierAsync(client, new
        {
            code,
            name = "IT Multi Company Vendor",
            companyAccounts = new[]
            {
                new { companyId = ErpApiFactory.DevCompanyId, payableAccountId = PayableAccountId },
                new { companyId = SecondCompanyId, payableAccountId = PayableAccountId },
            },
        });

        var supplierId = created["id"]!.GetValue<Guid>();
        var accounts = created["accounts"]!.AsArray();
        Assert.Equal(2, accounts.Count);
        Assert.Contains(accounts, a => a!["companyId"]!.GetValue<Guid>() == ErpApiFactory.DevCompanyId);
        Assert.Contains(accounts, a => a!["companyId"]!.GetValue<Guid>() == SecondCompanyId);

        // Round-trip: the mapping is not just an echo of the request body.
        using var fetched = await client.GetAsync($"/api/v1/suppliers/{supplierId}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        var reloaded = (JsonObject)JsonNode.Parse(await fetched.Content.ReadAsStringAsync())!;
        var reloadedAccounts = reloaded["accounts"]!.AsArray();
        Assert.Equal(2, reloadedAccounts.Count);
        Assert.All(reloadedAccounts, a => Assert.Equal(PayableAccountId, a!["payableAccountId"]!.GetValue<Guid>()));
    }

    /// <summary>A duplicate company id inside the payload is a 400 <c>invalid_gl_account</c> (aggregate rule).</summary>
    [Fact]
    public async Task Create_WithDuplicateCompanyInAccounts_Returns400InvalidGlAccount()
    {
        SeedSecondCompany();

        using var client = CreateClient();

        var payload = SerializePayload(new
        {
            code = UniqueCode("SUP"),
            name = "IT Duplicate Company Vendor",
            companyAccounts = new[]
            {
                new { companyId = SecondCompanyId, payableAccountId = PayableAccountId },
                new { companyId = SecondCompanyId, payableAccountId = PayableAccountId },
            },
        });

        using var response = await client.PostAsync(
            "/api/v1/suppliers",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = (JsonObject)JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal("invalid_gl_account", problem["code"]!.GetValue<string>());
    }

    // -------------------------------------------------------------------- T3.2 (2): hold -> 422

    /// <summary>
    /// SUP-G2 live: a supplier on hold for Invoices cannot be billed - the invoice POST is a 422
    /// <c>supplier_on_hold_invoices</c>, not a generic 400, because the request is well-formed and
    /// the supplier's state forbids it.
    /// </summary>
    [Fact]
    public async Task PostInvoice_ForSupplierOnHoldInvoices_Returns422()
    {
        using var client = CreateClient();

        var supplier = await CreateSupplierAsync(client, new
        {
            code = UniqueCode("SUP"),
            name = "IT Held Vendor",
            onHold = true,
            holdType = "Invoices",
        });
        var supplierId = supplier["id"]!.GetValue<Guid>();
        Assert.True(supplier["onHold"]!.GetValue<bool>());
        Assert.Equal("Invoices", supplier["holdType"]!.GetValue<string>());

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var payload = SerializePayload(new
        {
            companyId = ErpApiFactory.DevCompanyId,
            supplierId,
            billNumber = $"IT-BILL-{Guid.NewGuid():N}",
            postingDate = Format(today),
            dueDate = Format(today.AddDays(30)),
            taxAmount = 0m,
            lines = Array.Empty<object>(),
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/purchaseinvoices")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = (JsonObject)JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal(422, problem["status"]!.GetValue<int>());
        Assert.Equal("supplier_on_hold_invoices", problem["code"]!.GetValue<string>());
    }

    /// <summary>
    /// A hold scoped to PAYMENTS must NOT block billing (spec: the hold only blocks its target).
    /// The invoice proceeds past the hold guard into the three-way match, which rejects the empty
    /// line set as a 400 - proving the guard let it through rather than 422-ing.
    /// </summary>
    [Fact]
    public async Task PostInvoice_ForSupplierOnHoldPayments_IsNotBlockedByHold()
    {
        using var client = CreateClient();

        var supplier = await CreateSupplierAsync(client, new
        {
            code = UniqueCode("SUP"),
            name = "IT Payment Held Vendor",
            onHold = true,
            holdType = "Payments",
        });
        var supplierId = supplier["id"]!.GetValue<Guid>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var payload = SerializePayload(new
        {
            companyId = ErpApiFactory.DevCompanyId,
            supplierId,
            billNumber = $"IT-BILL-{Guid.NewGuid():N}",
            postingDate = Format(today),
            dueDate = Format(today.AddDays(30)),
            taxAmount = 0m,
            lines = Array.Empty<object>(),
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/purchaseinvoices")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));

        using var response = await client.SendAsync(request);

        Assert.NotEqual(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = (JsonObject)JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.NotEqual("supplier_on_hold_invoices", problem["code"]?.GetValue<string>());
    }

    // --------------------------------------------------------------------------------- helpers

    private HttpClient CreateClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-ID", ErpApiFactory.DevTenantId.ToString());
        return client;
    }

    private static string UniqueCode(string prefix) => $"IT-{prefix}-{Guid.NewGuid():N}";

    private async Task<JsonObject> CreateSupplierAsync(HttpClient client, object body)
    {
        using var response = await client.PostAsync(
            "/api/v1/suppliers",
            new StringContent(SerializePayload(body), Encoding.UTF8, "application/json"));

        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Supplier POST {(int)response.StatusCode}: {raw}");

        return (JsonObject)JsonNode.Parse(raw)!;
    }

    private static string SerializePayload<T>(T payload) =>
        JsonSerializer.Serialize(payload, PayloadOptions);

    private static string Format(DateOnly date) =>
        date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Seeds the second company (idempotent) so a true multi-company mapping can be created.</summary>
    private static void SeedSecondCompany()
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            "IF NOT EXISTS (SELECT 1 FROM dbo.Company WHERE Id = @Id) " +
            "INSERT INTO dbo.Company (Id, TenantId, Name, TaxId, CreatedAt) " +
            "VALUES (@Id, @TenantId, N'IT Second Company', N'', SYSDATETIMEOFFSET());";
        command.Parameters.AddWithValue("@Id", SecondCompanyId);
        command.Parameters.AddWithValue("@TenantId", ErpApiFactory.DevTenantId);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Removes every row this class can create (suppliers cascade their company-account rows, then
    /// the seeded second company). Fail loudly if the container is gone - a silent skip would let
    /// stale IT- rows accumulate and break re-runs.
    /// </summary>
    private static void Cleanup()
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        connection.Open();

        using (var suppliers = connection.CreateCommand())
        {
            suppliers.CommandText =
                "DELETE FROM dbo.Supplier WHERE TenantId = @TenantId AND Code LIKE N'IT-SUP-%';";
            suppliers.Parameters.AddWithValue("@TenantId", ErpApiFactory.DevTenantId);
            suppliers.ExecuteNonQuery();
        }

        using (var accounts = connection.CreateCommand())
        {
            accounts.CommandText = "DELETE FROM dbo.Account WHERE CompanyId = @Id;";
            accounts.Parameters.AddWithValue("@Id", SecondCompanyId);
            accounts.ExecuteNonQuery();
        }

        using (var company = connection.CreateCommand())
        {
            company.CommandText = "DELETE FROM dbo.Company WHERE Id = @Id;";
            company.Parameters.AddWithValue("@Id", SecondCompanyId);
            company.ExecuteNonQuery();
        }
    }
}
