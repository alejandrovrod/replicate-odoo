using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Erp.Api.IntegrationTests;

/// <summary>
/// CRM/ERPNext parity end to end over HTTP: the detail dossier read, the dossier edit
/// (lines replace + segmentation + totals), the ClosedWon quotation issue and the
/// read-only segmentation catalogs - against the LIVE dev container.
/// </summary>
/// <remarks>
/// <para><b>Customer prefix <c>CRMP-</c.</b> CrmLifecycleApiTests owns <c>CRM-IT-</c> codes
/// and CustomersApiTests deletes <c>IT-%</c>: a third prefix keeps every teardown disjoint.
/// Cleanup deletes activities, opportunity lines, quotations (+lines), opportunities, leads
/// and customers in FK order; catalog seeds and idempotency rows stay.</para>
/// </remarks>
[Collection(LedgerMutatingCollection.Name)]
public class CrmParityApiTests : IClassFixture<ErpApiFactory>, IDisposable
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    /// <summary>scripts/seed-dev-stock.sql: IT-001 Steel Bracket.</summary>
    private static readonly Guid ItemId = Guid.Parse("c0000000-0000-4000-8000-000000000001");

    /// <summary>scripts/seed-dev-crm.sql: NORTH territory / RETAIL industry / COMPETITOR reason.</summary>
    private static readonly Guid TerritoryNorthId = Guid.Parse("c2000000-0000-4000-8000-000000000001");
    private static readonly Guid IndustryRetailId = Guid.Parse("c3000000-0000-4000-8000-000000000002");
    private static readonly Guid LossReasonCompetitorId = Guid.Parse("c1000000-0000-4000-8000-000000000002");

    private readonly ErpApiFactory _factory;
    private readonly List<Guid> _leadIds = new();
    private readonly List<Guid> _opportunityIds = new();
    private readonly List<Guid> _customerIds = new();
    private readonly List<Guid> _quotationIds = new();

    public CrmParityApiTests(ErpApiFactory factory) => _factory = factory;

    // ------------------------------------------------------------------ detail

    /// <summary>
    /// The dossier read serves header money, empty lines for amount-only deals, the
    /// conversion activity and no quotations yet - and 404s a foreign deal.
    /// </summary>
    [Fact]
    public async Task Detail_ConvertedDeal_ReturnsDossierWithHistory()
    {
        using var client = CreateClient();
        var (_, opportunityId, _) = await SetupDealAsync(client, 50000m, 80m);

        using var response = await client.GetAsync(
            $"/api/v1/opportunities/{opportunityId}?companyId={ErpApiFactory.DevCompanyId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal("OPP-", detail["opportunityNumber"]!.GetValue<string>()[..4]);
        Assert.Equal(50000m, detail["opportunityAmount"]!.GetValue<decimal>());
        Assert.Equal("USD", detail["currencyCode"]!.GetValue<string>());
        Assert.Empty(detail["items"]!.AsArray());
        Assert.True(detail["activities"]!.AsArray().Count >= 1);
        Assert.Empty(detail["quotations"]!.AsArray());
        Assert.False(string.IsNullOrEmpty(detail["rowVersion"]!.GetValue<string>()));

        using var missing = await client.GetAsync(
            $"/api/v1/opportunities/{Guid.NewGuid()}?companyId={ErpApiFactory.DevCompanyId}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    // ------------------------------------------------------------------ update

    /// <summary>
    /// The dossier edit replaces lines (amount recalculates: 2 × 100 = 200), stores
    /// segmentation + competitors, and a stale RowVersion is a 409 with zero writes.
    /// </summary>
    [Fact]
    public async Task Update_LinesAndSegmentation_RecalculatesAndPersists()
    {
        using var client = CreateClient();
        var (_, opportunityId, _) = await SetupDealAsync(client, 50000m, 80m);
        var rowVersion = await ReadRowVersionAsync(client, opportunityId);

        using var updated = await PutWithKeyAsync(
            client,
            $"/api/v1/opportunities/{opportunityId}?companyId={ErpApiFactory.DevCompanyId}",
            new
            {
                opportunityId,
                companyId = ErpApiFactory.DevCompanyId,
                rowVersion,
                probability = 90m,
                territoryId = TerritoryNorthId,
                industryId = IndustryRetailId,
                marketSegment = "SMB",
                competitors = new[] { "Globex", "Initech" },
                items = new[] { new { itemId = ItemId, quantity = 2m, rate = 100m, discountPercentage = 0m } },
            });
        var updatedBody = await updated.Content.ReadAsStringAsync();
        Assert.True(updated.StatusCode == HttpStatusCode.OK, $"PUT {(int)updated.StatusCode}: {updatedBody}");

        var detail = JsonNode.Parse(updatedBody)!;
        Assert.Equal(200m, detail["opportunityAmount"]!.GetValue<decimal>());
        Assert.Equal(200m, detail["baseOpportunityAmount"]!.GetValue<decimal>());
        Assert.Equal(90m, detail["probability"]!.GetValue<decimal>());
        Assert.Equal("North", detail["territoryName"]!.GetValue<string>());
        Assert.Equal("Retail", detail["industryName"]!.GetValue<string>());
        Assert.Single(detail["items"]!.AsArray());

        var deal = await ReadDealAsync(opportunityId);
        Assert.Equal(200m, deal.Amount);

        // Stale token: 409 concurrency_conflict, the 200 above stays the stored state.
        using var stale = await PutWithKeyAsync(
            client,
            $"/api/v1/opportunities/{opportunityId}?companyId={ErpApiFactory.DevCompanyId}",
            new
            {
                opportunityId,
                companyId = ErpApiFactory.DevCompanyId,
                rowVersion,
                probability = 10m,
            });
        var problem = JsonNode.Parse(await stale.Content.ReadAsStringAsync())!;
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency_conflict", problem["code"]!.GetValue<string>());
        Assert.Equal(200m, (await ReadDealAsync(opportunityId)).Amount);
    }

    /// <summary>Scalar-only edits persist without touching lines (amount untouched).</summary>
    [Fact]
    public async Task Update_ScalarOnlyEdit_PersistsWithoutTouchingLines()
    {
        using var client = CreateClient();
        var (_, opportunityId, _) = await SetupDealAsync(client, 50000m, 80m);
        var rowVersion = await ReadRowVersionAsync(client, opportunityId);

        using var updated = await PutWithKeyAsync(
            client,
            $"/api/v1/opportunities/{opportunityId}?companyId={ErpApiFactory.DevCompanyId}",
            new
            {
                opportunityId,
                companyId = ErpApiFactory.DevCompanyId,
                rowVersion,
                probability = 60m,
            });
        var body = await updated.Content.ReadAsStringAsync();
        Assert.True(updated.StatusCode == HttpStatusCode.OK, $"PUT {(int)updated.StatusCode}: {body}");
        var edited = JsonNode.Parse(body)!;
        Assert.Equal(60m, edited["probability"]!.GetValue<decimal>());
        Assert.Equal(30000m, edited["weightedAmount"]!.GetValue<decimal>());
        Assert.Equal(50000m, edited["opportunityAmount"]!.GetValue<decimal>());
    }

    /// <summary>Editing a closed deal is a 409 <c>crm_opportunity_not_open</c>.</summary>
    [Fact]
    public async Task Update_ClosedDeal_Returns409NotOpen()
    {
        using var client = CreateClient();
        var (_, opportunityId, _) = await SetupDealAsync(client, 50000m, 80m);
        await AdvanceAsync(client, opportunityId, "ClosedWon");
        var rowVersion = await ReadRowVersionAsync(client, opportunityId);

        using var response = await PutWithKeyAsync(
            client,
            $"/api/v1/opportunities/{opportunityId}?companyId={ErpApiFactory.DevCompanyId}",
            new
            {
                opportunityId,
                companyId = ErpApiFactory.DevCompanyId,
                rowVersion,
                probability = 10m,
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal("crm_opportunity_not_open", problem["code"]!.GetValue<string>());
    }

    // ------------------------------------------------------------------ quotation

    /// <summary>
    /// A ClosedWon deal with priced lines issues a Draft quotation (201 + Location):
    /// lines drag with catalog names, totals stay server-computed, a history note lands,
    /// and the detail read lists the quotation.
    /// </summary>
    [Fact]
    public async Task CreateQuotation_FromWonDealWithLines_IssuesDraftQuotation()
    {
        using var client = CreateClient();
        var (_, opportunityId, _) = await SetupDealAsync(client, 50000m, 80m);
        var rowVersion = await ReadRowVersionAsync(client, opportunityId);

        using var updated = await PutWithKeyAsync(
            client,
            $"/api/v1/opportunities/{opportunityId}?companyId={ErpApiFactory.DevCompanyId}",
            new
            {
                opportunityId,
                companyId = ErpApiFactory.DevCompanyId,
                rowVersion,
                items = new[] { new { itemId = ItemId, quantity = 2m, rate = 100m, discountPercentage = 0m } },
            });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        await AdvanceAsync(client, opportunityId, "ClosedWon");

        using var issued = await PostWithKeyAsync(
            client,
            $"/api/v1/opportunities/{opportunityId}/create-quotation?companyId={ErpApiFactory.DevCompanyId}",
            new { createdByUserId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Created, issued.StatusCode);
        Assert.NotNull(issued.Headers.Location);

        var quotation = JsonNode.Parse(await issued.Content.ReadAsStringAsync())!;
        var quotationId = quotation["id"]!.GetValue<Guid>();
        _quotationIds.Add(quotationId);
        Assert.Matches(@"^QTN-\d{4}-\d{5}$", quotation["quotationNo"]!.GetValue<string>());
        Assert.Equal("Draft", quotation["status"]!.GetValue<string>());
        Assert.Equal(200m, quotation["totalAmount"]!.GetValue<decimal>());
        var line = Assert.Single(quotation["items"]!.AsArray());
        Assert.Equal(ItemId, line!["itemId"]!.GetValue<Guid>());
        Assert.Equal("Steel Bracket", line["itemName"]!.GetValue<string>());

        // The quotation read serves the same document by id.
        using var fetched = await client.GetAsync(
            $"/api/v1/quotations/{quotationId}?companyId={ErpApiFactory.DevCompanyId}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);

        // The deal history carries the issue note; the detail lists the quotation.
        Assert.True(await HasQuotationNoteAsync(opportunityId, quotation["quotationNo"]!.GetValue<string>()));
        using var detail = await client.GetAsync(
            $"/api/v1/opportunities/{opportunityId}?companyId={ErpApiFactory.DevCompanyId}");
        var quotations = JsonNode.Parse(await detail.Content.ReadAsStringAsync())!["quotations"]!.AsArray();
        Assert.Single(quotations);
    }

    /// <summary>Quoting an open deal is a 409 <c>crm_opportunity_not_won</c>, zero writes.</summary>
    [Fact]
    public async Task CreateQuotation_FromOpenDeal_Returns409NotWon()
    {
        using var client = CreateClient();
        var (_, opportunityId, _) = await SetupDealAsync(client, 50000m, 80m);

        using var response = await PostWithKeyAsync(
            client,
            $"/api/v1/opportunities/{opportunityId}/create-quotation?companyId={ErpApiFactory.DevCompanyId}",
            new { });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal("crm_opportunity_not_won", problem["code"]!.GetValue<string>());
        Assert.Equal(0, await CountQuotationsAsync(opportunityId));
    }

    // ------------------------------------------------------------------ catalogs + close

    /// <summary>The three read-only catalogs serve the seeded vocabulary.</summary>
    [Fact]
    public async Task Catalogs_ReturnSeededRows()
    {
        using var client = CreateClient();

        Assert.Contains(await GetCatalogAsync(client, "loss-reasons"), n => n!["code"]!.GetValue<string>() == "PRICE");
        Assert.Contains(await GetCatalogAsync(client, "territories"), n => n!["code"]!.GetValue<string>() == "NORTH");
        Assert.Contains(await GetCatalogAsync(client, "industries"), n => n!["code"]!.GetValue<string>() == "RETAIL");
    }

    /// <summary>
    /// Closing with a catalog reason persists the FK plus the free-text notes (and an
    /// unknown reason id is a 400 <c>crm_loss_reason_not_found</c>).
    /// </summary>
    [Fact]
    public async Task CloseLost_WithCatalogReason_PersistsReasonIdAndNotes()
    {
        using var client = CreateClient();
        var (_, opportunityId, _) = await SetupDealAsync(client, 50000m, 80m);

        using var lost = await PostWithKeyAsync(
            client,
            $"/api/v1/opportunities/{opportunityId}/advance?companyId={ErpApiFactory.DevCompanyId}",
            new { toStage = "ClosedLost", lossReason = "Priced 15% lower", lossReasonId = LossReasonCompetitorId });
        Assert.Equal(HttpStatusCode.OK, lost.StatusCode);

        var deal = await ReadDealAsync(opportunityId);
        Assert.Equal(LossReasonCompetitorId, deal.LossReasonId);
        Assert.Equal("Priced 15% lower", deal.LossReason);

        var (_, secondId, _) = await SetupDealAsync(client, 10000m, 20m);
        using var unknown = await PostWithKeyAsync(
            client,
            $"/api/v1/opportunities/{secondId}/advance?companyId={ErpApiFactory.DevCompanyId}",
            new { toStage = "ClosedLost", lossReasonId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        var problem = JsonNode.Parse(await unknown.Content.ReadAsStringAsync())!;
        Assert.Equal("crm_loss_reason_not_found", problem["code"]!.GetValue<string>());
    }

    // ------------------------------------------------------------------------------- helpers

    private HttpClient CreateClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-ID", ErpApiFactory.DevTenantId.ToString());
        return client;
    }

    private static Task<HttpResponseMessage> PostWithKeyAsync(
        HttpClient client, string url, object? payload = null, string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        request.Headers.Add(IdempotencyKeyHeader, key ?? Guid.NewGuid().ToString("N"));
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> PutWithKeyAsync(HttpClient client, string url, object payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add(IdempotencyKeyHeader, Guid.NewGuid().ToString("N"));
        return client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> AdvanceAsync(HttpClient client, Guid opportunityId, string toStage)
    {
        using var response = await PostWithKeyAsync(
            client,
            $"/api/v1/opportunities/{opportunityId}/advance?companyId={ErpApiFactory.DevCompanyId}",
            new { toStage });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return response;
    }

    /// <summary>Ingests one lead and converts it: a live open deal owned by this test.</summary>
    private async Task<(Guid LeadId, Guid OpportunityId, Guid CustomerId)> SetupDealAsync(
        HttpClient client, decimal amount, decimal probability)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        using var ingest = await PostWithKeyAsync(
            client, "/api/v1/leads/ingest",
            new
            {
                companyId = ErpApiFactory.DevCompanyId,
                leadCode = $"CRMP-{suffix}-L1",
                leadName = "Parity Buyer",
                organizationName = "ParityCorp",
                source = "Website",
                deduplicationKey = $"crmp-{suffix}-web-1",
            });
        Assert.Equal(HttpStatusCode.Created, ingest.StatusCode);
        var leadId = JsonNode.Parse(await ingest.Content.ReadAsStringAsync())!["lead"]!["id"]!.GetValue<Guid>();

        using var convert = await PostWithKeyAsync(
            client, $"/api/v1/leads/{leadId}/convert?companyId={ErpApiFactory.DevCompanyId}",
            new
            {
                customerCode = $"CRMP-{suffix}-C1",
                paymentTermsDays = 30,
                opportunityAmount = amount,
                opportunityProbability = probability,
                expectedClosingDate = "2027-06-30",
                convertedByUserId = Guid.NewGuid(),
            });
        Assert.Equal(HttpStatusCode.OK, convert.StatusCode);
        var converted = JsonNode.Parse(await convert.Content.ReadAsStringAsync())!;

        var opportunityId = converted["opportunityId"]!.GetValue<Guid>();
        _leadIds.Add(leadId);
        _opportunityIds.Add(opportunityId);
        _customerIds.Add(converted["customerId"]!.GetValue<Guid>());
        return (leadId, opportunityId, converted["customerId"]!.GetValue<Guid>());
    }

    private static async Task<string> ReadRowVersionAsync(HttpClient client, Guid opportunityId)
    {
        using var response = await client.GetAsync(
            $"/api/v1/opportunities/{opportunityId}?companyId={ErpApiFactory.DevCompanyId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!["rowVersion"]!.GetValue<string>();
    }

    private static async Task<JsonArray> GetCatalogAsync(HttpClient client, string catalog)
    {
        using var response = await client.GetAsync($"/api/v1/{catalog}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsArray();
    }

    private sealed record DealRow(decimal Amount, Guid? LossReasonId, string? LossReason);

    private static async Task<DealRow> ReadDealAsync(Guid opportunityId)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT OpportunityAmount, LossReasonId, LossReason FROM dbo.Opportunity "
            + "WHERE Id = @Id AND TenantId = @TenantId;", connection);
        command.Parameters.AddWithValue("@Id", opportunityId);
        command.Parameters.AddWithValue("@TenantId", ErpApiFactory.DevTenantId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new DealRow(
            reader.GetDecimal(0),
            await reader.IsDBNullAsync(1) ? null : reader.GetGuid(1),
            await reader.IsDBNullAsync(2) ? null : reader.GetString(2));
    }

    private static async Task<bool> HasQuotationNoteAsync(Guid opportunityId, string quotationNo)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT COUNT_BIG(*) FROM dbo.CRMActivity WHERE OpportunityId = @Id AND Subject LIKE @Pattern;",
            connection);
        command.Parameters.AddWithValue("@Id", opportunityId);
        command.Parameters.AddWithValue("@Pattern", $"%{quotationNo}%");
        return (long)(await command.ExecuteScalarAsync())! > 0;
    }

    private static async Task<int> CountQuotationsAsync(Guid opportunityId)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT COUNT_BIG(*) FROM dbo.Quotations WHERE OpportunityId = @Id;", connection);
        command.Parameters.AddWithValue("@Id", opportunityId);
        return checked((int)(long)(await command.ExecuteScalarAsync())!);
    }

    /// <summary>Removes this class's rows in FK order (quotations + lines stay out of other suites via CRMP- owners).</summary>
    public void Dispose()
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            "DECLARE @leads TABLE (Id UNIQUEIDENTIFIER); "
            + "INSERT INTO @leads SELECT Id FROM dbo.Lead "
            + "WHERE TenantId = @TenantId AND LeadCode LIKE N'CRMP-%'; "
            + "DECLARE @opps TABLE (Id UNIQUEIDENTIFIER); "
            + "INSERT INTO @opps SELECT Id FROM dbo.Opportunity WHERE PartyId IN (SELECT Id FROM @leads); "
            + "INSERT INTO @opps SELECT DISTINCT OpportunityId FROM dbo.Quotations "
            + "WHERE OpportunityId IS NOT NULL AND CustomerId IN ("
            + "SELECT Id FROM dbo.Customer WHERE TenantId = @TenantId AND CustomerCode LIKE N'CRMP-%'); "
            + "DELETE FROM dbo.CRMActivity WHERE OpportunityId IN (SELECT Id FROM @opps); "
            + "DELETE FROM dbo.QuotationItems "
            + "WHERE QuotationId IN (SELECT Id FROM dbo.Quotations WHERE OpportunityId IN (SELECT Id FROM @opps)); "
            + "DELETE FROM dbo.Quotations WHERE OpportunityId IN (SELECT Id FROM @opps); "
            + "DELETE FROM dbo.OpportunityItem WHERE OpportunityId IN (SELECT Id FROM @opps); "
            + "DELETE FROM dbo.Opportunity WHERE Id IN (SELECT Id FROM @opps); "
            + "DELETE FROM dbo.Lead WHERE Id IN (SELECT Id FROM @leads); "
            + "DELETE FROM dbo.Customer WHERE TenantId = @TenantId AND CustomerCode LIKE N'CRMP-%';";
        command.Parameters.AddWithValue("@TenantId", ErpApiFactory.DevTenantId);
        command.ExecuteNonQuery();
    }
}
