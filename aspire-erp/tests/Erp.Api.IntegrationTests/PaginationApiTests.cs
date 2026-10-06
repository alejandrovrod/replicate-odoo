using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Erp.Api.IntegrationTests;

/// <summary>
/// Standard Pagination Pattern acceptance: every flat list endpoint returns the
/// <c>PagedResult</c> envelope (<c>items</c> + <c>totalCount</c>/<c>pageNumber</c>/
/// <c>pageSize</c>/<c>totalPages</c>), pages are disjoint and stable, and out-of-range
/// inputs normalize (page 0 -&gt; 1, size 0 -&gt; 50, size &gt; 500 -&gt; 500).
/// Suppliers are the vehicle: tenant-wide, no company scoping, no ledger writes, so this
/// class stays OUT of <see cref="LedgerMutatingCollection"/> like <see cref="CustomersApiTests"/>.
/// </summary>
public class PaginationApiTests : IClassFixture<ErpApiFactory>
{
    private readonly ErpApiFactory _factory;

    public PaginationApiTests(ErpApiFactory factory) => _factory = factory;

    [Fact]
    public async Task SuppliersList_ReturnsPagedEnvelopeWithDisjointStablePages()
    {
        using var client = CreateClient();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var codes = Enumerable.Range(0, 5).Select(i => $"PG-{tag}-{i:00}").ToList();

        foreach (var code in codes)
        {
            using var created = await client.PostAsJsonAsync("/api/v1/suppliers", new
            {
                code,
                name = $"Pagination Probe {code}",
            });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        static async Task<JsonNode> GetPageAsync(HttpClient http, int page, int pageSize)
        {
            using var response = await http.GetAsync($"/api/v1/suppliers?page={page}&pageSize={pageSize}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        }

        var first = await GetPageAsync(client, 1, 2);
        Assert.Equal(2, first["items"]!.AsArray().Count);
        Assert.Equal(1, first["pageNumber"]!.GetValue<int>());
        Assert.Equal(2, first["pageSize"]!.GetValue<int>());

        var totalCount = first["totalCount"]!.GetValue<int>();
        Assert.True(totalCount >= 5);
        Assert.Equal((int)Math.Ceiling(totalCount / 2.0), first["totalPages"]!.GetValue<int>());

        var second = await GetPageAsync(client, 2, 2);
        Assert.Equal(2, second["items"]!.AsArray().Count);

        var firstIds = first["items"]!.AsArray().Select(n => n!["id"]!.GetValue<string>()).ToHashSet();
        var secondIds = second["items"]!.AsArray().Select(n => n!["id"]!.GetValue<string>()).ToHashSet();
        Assert.Empty(firstIds.Intersect(secondIds));

        // Our five rows surface across the first pages (ordering is newest-first, shared DB).
        var seen = first["items"]!.AsArray().Concat(second["items"]!.AsArray())
            .Select(n => n!["code"]!.GetValue<string>())
            .ToHashSet();
        foreach (var missing in codes.Where(c => !seen.Contains(c)))
        {
            var found = false;
            for (var page = 3; page <= first["totalPages"]!.GetValue<int>(); page++)
            {
                var later = await GetPageAsync(client, page, 2);
                if (later["items"]!.AsArray().Any(n => n!["code"]!.GetValue<string>() == missing))
                {
                    found = true;
                    break;
                }
            }

            Assert.True(found, $"Created supplier {missing} never surfaced in any page.");
        }
    }

    [Fact]
    public async Task SuppliersList_NormalizesOutOfRangePaging()
    {
        using var client = CreateClient();

        using var zeroPage = await client.GetAsync("/api/v1/suppliers?page=0&pageSize=2");
        Assert.Equal(HttpStatusCode.OK, zeroPage.StatusCode);
        var zeroPageBody = JsonNode.Parse(await zeroPage.Content.ReadAsStringAsync())!;
        Assert.Equal(1, zeroPageBody["pageNumber"]!.GetValue<int>());

        using var zeroSize = await client.GetAsync("/api/v1/suppliers?page=1&pageSize=0");
        Assert.Equal(HttpStatusCode.OK, zeroSize.StatusCode);
        var zeroSizeBody = JsonNode.Parse(await zeroSize.Content.ReadAsStringAsync())!;
        Assert.Equal(50, zeroSizeBody["pageSize"]!.GetValue<int>());

        using var hugeSize = await client.GetAsync("/api/v1/suppliers?page=1&pageSize=9999");
        Assert.Equal(HttpStatusCode.OK, hugeSize.StatusCode);
        var hugeSizeBody = JsonNode.Parse(await hugeSize.Content.ReadAsStringAsync())!;
        Assert.Equal(500, hugeSizeBody["pageSize"]!.GetValue<int>());
    }

    /// <summary>
    /// Flat warehouses endpoint: card grids consume rows (never the tree), with working
    /// <c>leavesOnly</c> filter and the same envelope contract.
    /// </summary>
    [Fact]
    public async Task WarehousesFlatList_LeavesOnly_ReturnsPagedLedgerWarehouses()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(
            $"/api/v1/warehouses?companyId={ErpApiFactory.DevCompanyId}&leavesOnly=true&page=1&pageSize=6");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var items = body["items"]!.AsArray();
        Assert.True(items.Count > 0);
        Assert.Equal(1, body["pageNumber"]!.GetValue<int>());
        Assert.Equal(6, body["pageSize"]!.GetValue<int>());
        Assert.True(body["totalCount"]!.GetValue<int>() >= items.Count);
        Assert.All(items, node => Assert.False(node!["isGroup"]!.GetValue<bool>()));
    }

    /// <summary>
    /// Stock summary endpoint: global stat cards come from scalar aggregates, never from rows.
    /// </summary>
    [Fact]
    public async Task StockSummary_ReturnsAggregateSnapshot()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(
            $"/api/v1/stock/summary?companyId={ErpApiFactory.DevCompanyId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.True(body["totalSkus"]!.GetValue<int>() >= 0);
        Assert.True(body["activeSkus"]!.GetValue<int>() >= 0);
        Assert.True(body["warehouseCount"]!.GetValue<int>() > 0);
        Assert.True(body["leafWarehouseCount"]!.GetValue<int>() > 0);
        Assert.True(body["leafWarehouseCount"]!.GetValue<int>() <= body["warehouseCount"]!.GetValue<int>());
        _ = body["totalValue"]!.GetValue<decimal>();
    }

    /// <summary>Tenant-scoped client (X-Tenant-ID only - see ErpApiFactory remarks on auth).</summary>
    private HttpClient CreateClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-ID", ErpApiFactory.DevTenantId.ToString());
        return client;
    }
}
