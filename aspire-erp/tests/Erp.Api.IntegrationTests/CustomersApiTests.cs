using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Erp.Api.IntegrationTests;

/// <summary>
/// Task 5.1 acceptance: HTTP integration tests for the customer contract the Selling module
/// exposes (and Task 5.5 consumes): POST round-trips 201 Created -> EF insert ->
/// UQ_Customer_Tenant_Company_Code -> GET read model -> CustomerDto serialization, a duplicate
/// code is a 409, the list is filtered by company AND tenant, and an unknown id is a 404.
/// </summary>
/// <remarks>
/// A single test class means xunit runs its [Fact]s SEQUENTIALLY against the shared
/// <see cref="ErpApiFactory"/> fixture (same determinism rationale as AccountsApiTests). Customer
/// creation mutates NO ledger row, so this class deliberately stays OUT of the
/// <see cref="LedgerMutatingCollection"/> - it neither appends GLEntry nor flips the shared
/// FrozenAccountsDate.
/// </remarks>
public class CustomersApiTests : IClassFixture<ErpApiFactory>, IDisposable
{
    private readonly ErpApiFactory _factory;

    public CustomersApiTests(ErpApiFactory factory) => _factory = factory;

    [Fact]
    public async Task PostCustomer_Returns201WithLocationAndRoundtripsThroughGetById()
    {
        using var client = CreateClient(ErpApiFactory.DevTenantId);
        var code = $"IT-{Guid.NewGuid().ToString("N")[..8]}";

        var response = await client.PostAsJsonAsync("/api/v1/customers", new
        {
            companyId = ErpApiFactory.DevCompanyId,
            customerCode = code,
            customerName = "Integration Test Buyer",
            taxId = "TAX-IT-1",
            creditLimit = 5000.00m,
            bypassCreditLimitCheck = false,
            billingCurrency = "USD",
            paymentTermsDays = 30,
            isActive = true,
        });

        // 201 Created + Location of the freshly created resource (mirrors SuppliersController).
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var created = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var createdId = created["id"]!.GetValue<Guid>();
        Assert.Equal(code, created["code"]!.GetValue<string>());
        Assert.Equal("Integration Test Buyer", created["name"]!.GetValue<string>());
        Assert.Equal(ErpApiFactory.DevCompanyId, created["companyId"]!.GetValue<Guid>());
        Assert.Equal(5000.00m, created["creditLimit"]!.GetValue<decimal>());
        Assert.False(created["bypassCreditLimitCheck"]!.GetValue<bool>());
        Assert.Equal(0m, created["outstandingAmount"]!.GetValue<decimal>()); // only postings move it

        // Roundtrip: GET by id returns the same customer with the id the POST returned.
        using var getResponse = await client.GetAsync(
            $"/api/v1/customers/{createdId}?companyId={ErpApiFactory.DevCompanyId}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = JsonNode.Parse(await getResponse.Content.ReadAsStringAsync())!;
        Assert.Equal(createdId, fetched["id"]!.GetValue<Guid>());
        Assert.Equal(code, fetched["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task PostCustomer_DuplicateCode_Returns409WithDuplicateCustomerCode()
    {
        using var client = CreateClient(ErpApiFactory.DevTenantId);
        var code = $"IT-{Guid.NewGuid().ToString("N")[..8]}";

        var payload = new
        {
            companyId = ErpApiFactory.DevCompanyId,
            customerCode = code,
            customerName = "Integration Test Buyer",
        };

        using var first = await client.PostAsJsonAsync("/api/v1/customers", payload);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Same status/code family as SuppliersController's duplicate: 409 + snake_case code.
        using var second = await client.PostAsJsonAsync("/api/v1/customers", payload);
        var problem = await AssertProblemAsync(
            second,
            HttpStatusCode.Conflict,
            "Duplicate Customer Code",
            "duplicate_customer_code");

        Assert.Contains(code, problem["detail"]!.GetValue<string>());
    }

    [Fact]
    public async Task GetCustomers_Returns200FilteredByCompanyAndTenant()
    {
        using var client = CreateClient(ErpApiFactory.DevTenantId);
        var code = $"IT-{Guid.NewGuid().ToString("N")[..8]}";

        using var created = await client.PostAsJsonAsync("/api/v1/customers", new
        {
            companyId = ErpApiFactory.DevCompanyId,
            customerCode = code,
            customerName = "Integration Test Buyer",
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // The created customer is visible through the company-scoped list.
        using var response = await client.GetAsync(
            $"/api/v1/customers?companyId={ErpApiFactory.DevCompanyId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var list = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsArray();
        Assert.Contains(list, node => node!["code"]?.GetValue<string>() == code);

        // Company scoping: a company that owns no customers comes back EMPTY.
        using var otherCompany = await client.GetAsync(
            $"/api/v1/customers?companyId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.OK, otherCompany.StatusCode);
        Assert.Empty(JsonNode.Parse(await otherCompany.Content.ReadAsStringAsync())!.AsArray());

        // Tenant scoping (Constitution Article II.3): the SAME companyId seen from a foreign
        // tenant must come back EMPTY - no cross-tenant leakage.
        using var foreignClient = CreateClient(ErpApiFactory.ForeignTenantId);
        using var foreignResponse = await foreignClient.GetAsync(
            $"/api/v1/customers?companyId={ErpApiFactory.DevCompanyId}");
        Assert.Equal(HttpStatusCode.OK, foreignResponse.StatusCode);
        Assert.Empty(JsonNode.Parse(await foreignResponse.Content.ReadAsStringAsync())!.AsArray());
    }

    [Fact]
    public async Task GetCustomerById_UnknownId_Returns404WithCustomerNotFound()
    {
        using var client = CreateClient(ErpApiFactory.DevTenantId);

        using var response = await client.GetAsync(
            $"/api/v1/customers/{Guid.NewGuid()}?companyId={ErpApiFactory.DevCompanyId}");

        await AssertProblemAsync(
            response,
            HttpStatusCode.NotFound,
            "Customer Not Found",
            "customer_not_found");
    }

    /// <summary>Tenant-scoped client: only X-Tenant-ID (see ErpApiFactory remarks - auth is NOT disabled).</summary>
    private HttpClient CreateClient(Guid tenantId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-ID", tenantId.ToString());
        return client;
    }

    /// <summary>
    /// Asserts the RFC 7807 contract of a rejection: status line, <c>status</c> extension, title
    /// and the stable machine code in the <c>code</c> extension (mirrors
    /// JournalEntriesApiTests.AssertProblemAsync). Returns the parsed problem for extra
    /// detail assertions.
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

    /// <summary>
    /// Removes the `IT-` customers created during this class's run so re-runs stay idempotent
    /// (the fixture's Dispose only cleans up accounts - it knows nothing about the Customer table
    /// this task introduced).
    /// </summary>
    public void Dispose()
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            "DELETE FROM dbo.Customer WHERE TenantId = @TenantId AND CustomerCode LIKE N'IT-%';";
        command.Parameters.AddWithValue("@TenantId", ErpApiFactory.DevTenantId);
        command.ExecuteNonQuery();
    }
}
