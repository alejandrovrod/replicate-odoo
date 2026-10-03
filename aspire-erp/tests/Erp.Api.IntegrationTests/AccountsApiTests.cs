using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Erp.Api.IntegrationTests;

/// <summary>
/// Task 1.3 acceptance: HTTP integration tests return 200 OK with a tenant-scoped account tree,
/// and POST /api/v1/accounts round-trips through the real write path (201 Created -> EF insert ->
/// UQ_Account_Tenant_Company_Code -> tree read model -> AccountDto serialization).
/// </summary>
/// <remarks>
/// A single test class means xunit runs its [Fact]s SEQUENTIALLY against the shared
/// <see cref="ErpApiFactory"/> fixture, so the roundtrip assertions and the fixture's `IT-`
/// cleanup stay deterministic (no parallel writes racing the unique index inside one run).
/// </remarks>
public class AccountsApiTests : IClassFixture<ErpApiFactory>
{
    private readonly ErpApiFactory _factory;

    public AccountsApiTests(ErpApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetTree_WithTenantHeader_Returns200WithTenantScopedTree()
    {
        using var client = CreateClient(ErpApiFactory.DevTenantId);

        var response = await client.GetAsync(
            $"/api/v1/accounts/tree?companyId={ErpApiFactory.DevCompanyId}");

        // Task 1.3 acceptance: 200 OK with a non-empty, fully-populated account tree.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var tree = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsArray();
        Assert.NotEmpty(tree); // the seeded COA is visible to its own tenant

        var flat = Flatten(tree);
        Assert.NotEmpty(flat);
        foreach (var node in flat)
        {
            // "all nodes have ids" - plus code/name so an empty shell would fail too.
            Assert.NotEqual(Guid.Empty, node["id"]!.GetValue<Guid>());
            Assert.False(string.IsNullOrWhiteSpace(node["code"]?.GetValue<string>()));
            Assert.False(string.IsNullOrWhiteSpace(node["name"]?.GetValue<string>()));
        }

        // Tenant scoping (Constitution Article II.3): the SAME companyId seen from a tenant that
        // owns no COA must come back as an EMPTY tree - no cross-tenant leakage.
        using var foreignClient = CreateClient(ErpApiFactory.ForeignTenantId);
        var foreignResponse = await foreignClient.GetAsync(
            $"/api/v1/accounts/tree?companyId={ErpApiFactory.DevCompanyId}");

        Assert.Equal(HttpStatusCode.OK, foreignResponse.StatusCode);
        Assert.Empty(JsonNode.Parse(await foreignResponse.Content.ReadAsStringAsync())!.AsArray());
    }

    [Fact]
    public async Task PostAccount_Returns201AndRoundtripsIntoTree()
    {
        using var client = CreateClient(ErpApiFactory.DevTenantId);

        // Unique code per run (IT- prefix is what the fixture cleans up in Dispose).
        var code = $"IT-{Guid.NewGuid().ToString("N")[..8]}"; // "IT-" + 8 hex chars (NVARCHAR(50) limit)
        var command = new
        {
            companyId = ErpApiFactory.DevCompanyId,
            accountCode = code,
            accountName = "Integration Test Account",
            rootType = "Asset",
            isGroup = false,
            parentAccountId = (Guid?)null,
            currency = "USD",
            isActive = true,
            type = "Cash", // Task 1.1: optional Type round-trips through the DTO as "type"
        };

        var response = await client.PostAsJsonAsync("/api/v1/accounts", command);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var createdId = created["id"]!.GetValue<Guid>();
        Assert.Equal(code, created["code"]!.GetValue<string>());
        Assert.Equal("Asset", created["rootType"]!.GetValue<string>());
        Assert.Equal("Cash", created["type"]!.GetValue<string>());

        // The DTO must mirror what was actually PERSISTED in Type NVARCHAR(50) - proves the
        // enum->NAME conversion and that EF did not silently drop the value on insert.
        Assert.Equal("Cash", await ReadPersistedTypeAsync(createdId));

        // Roundtrip: the new account shows up in the tree (write path + read model + DTO), at
        // root level, carrying the id the POST returned.
        var treeResponse = await client.GetAsync(
            $"/api/v1/accounts/tree?companyId={ErpApiFactory.DevCompanyId}");
        Assert.Equal(HttpStatusCode.OK, treeResponse.StatusCode);

        var flat = Flatten(JsonNode.Parse(await treeResponse.Content.ReadAsStringAsync())!.AsArray());
        var node = flat.SingleOrDefault(n => n["code"]?.GetValue<string>() == code);
        Assert.NotNull(node);
        Assert.Equal(createdId, node!["id"]!.GetValue<Guid>());
    }

    /// <summary>
    /// Tenant-scoped client: only X-Tenant-ID, mirroring the e2e scripts' contract with the
    /// requirement-only TenantMember policy (see ErpApiFactory remarks - auth is NOT disabled).
    /// </summary>
    private HttpClient CreateClient(Guid tenantId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-ID", tenantId.ToString());
        return client;
    }

    /// <summary>Depth-first flatten of the nested tree DTO (nodes without children are leaves).</summary>
    private static List<JsonNode> Flatten(JsonArray nodes)
    {
        var flat = new List<JsonNode>();
        foreach (var node in nodes)
        {
            flat.Add(node!);

            if (node!["children"] is JsonArray children)
            {
                flat.AddRange(Flatten(children));
            }
        }

        return flat;
    }

    /// <summary>Reads the stored Type NAME straight from the live dev container.</summary>
    private static async Task<string> ReadPersistedTypeAsync(Guid accountId)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT [Type] FROM dbo.Account WHERE Id = @Id;";
        command.Parameters.AddWithValue("@Id", accountId);

        return (string)(await command.ExecuteScalarAsync())!;
    }
}
