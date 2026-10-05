using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Erp.Api.IntegrationTests;

/// <summary>
/// Task 11.7 / specs CRM-01..CRM-06 live: the full ingest -&gt; convert -&gt; advance -&gt;
/// ClosedWon lifecycle with weighted-math assertions, the CRM-04 webhook replay guards
/// (HTTP idempotency-key replay AND body-level DeduplicationKey dedup), the CRM-02/CRM-03
/// close rules (loss reason required, double-close 409), the CRM-05 re-open flow with
/// history intact, and the CRM-06 RowVersion race - end to end over HTTP against the
/// LIVE dev container.
/// </summary>
/// <remarks>
/// <para><b>The oracle is the DATABASE, not only the response.</b> Every test snapshots the
/// relevant row counts around the ACT and asserts DELTAS - never absolute totals, which
/// other runs legitimately append to (the banking/manufacturing/payroll pattern).
/// Serialized through <see cref="LedgerMutatingCollection"/> per the Block C mandate.</para>
/// <para><b>Masters are per-test rows, never shared.</b> Leads, opportunities, activities and
/// customers use fresh <c>CRM-IT-</c> codes on every run, deleted in a <c>finally</c> block
/// in FK order (activities -&gt; opportunities -&gt; leads -&gt; customers), so re-runs stay
/// hermetic. The <c>CRM-IT-</c> prefix deliberately differs from the shared <c>IT-</c> sweep
/// of <c>CustomersApiTests.Dispose</c> so a parallel class teardown can never reap our
/// converted customers mid-test. Seed rows (<c>LEAD-DEMO-001</c> /
/// <c>OPP-2026-09901</c>) are never touched or asserted on.</para>
/// <para><b>Provisioning stays neutral.</b> CRM posts no ledger rows; every per-test business
/// row is removed by cleanup.</para>
/// </remarks>
[Collection(LedgerMutatingCollection.Name)]
public class CrmLifecycleApiTests : IClassFixture<ErpApiFactory>, IDisposable
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    private readonly ErpApiFactory _factory;

    /// <summary>scripts/seed-dev-stock.sql: IT-001 Steel Bracket - the catalog item the C2 live tests order.</summary>
    private static readonly Guid SeededItemId = Guid.Parse("c0000000-0000-4000-8000-000000000001");

    /// <summary>Every per-test row this class created, for FK-order cleanup at Dispose.</summary>
    private readonly List<Guid> _leadIds = new();
    private readonly List<Guid> _opportunityIds = new();
    private readonly List<Guid> _customerIds = new();
    private readonly List<Guid> _salesOrderIds = new();

    public CrmLifecycleApiTests(ErpApiFactory factory) => _factory = factory;

    // ------------------------------------------------- CRM-01/02/03 full lifecycle live

    /// <summary>
    /// Full lifecycle live: ingest -&gt; convert (Customer + Opportunity + Converted + conversion
    /// note on the deal, weighted 15000 x 25% = 3750) -&gt; Proposal (50% / 7500) -&gt;
    /// Negotiation (80% / 12000) -&gt; ClosedWon (100% / 15000). Every hop is asserted on the
    /// response AND the row.
    /// </summary>
    [Fact]
    public async Task FullLifecycle_IngestConvertAdvanceToWon_WeightedMathHoldsAtEveryStage()
    {
        using var client = CreateClient();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var leadCode = $"CRM-IT-{tag}-L1";
        var customerCode = $"CRM-IT-{tag}-C1";

        // Ingest: fresh webhook intake is a 201 with Duplicate=false.
        var ingest = await PostIngestAsync(client, leadCode, $"crm-it-{tag}-web-1", "Alex Rivera");
        Assert.Equal(HttpStatusCode.Created, ingest.StatusCode);
        var ingestBody = JsonNode.Parse(await ingest.Content.ReadAsStringAsync())!;
        Assert.False(ingestBody["duplicate"]!.GetValue<bool>());
        var leadId = ingestBody["lead"]!["id"]!.GetValue<Guid>();
        Assert.Equal(leadCode, ingestBody["lead"]!["leadCode"]!.GetValue<string>());
        _leadIds.Add(leadId);
        Assert.Equal("Open", await ReadLeadStatusAsync(leadId));

        // Convert: Customer + Opportunity + Converted, weighted 15000 x 25% = 3750.
        // convertedByUserId is the note author (spec CRM-03 audit trail): without it the
        // handler commits Customer+Opportunity+Converted but omits the activity note.
        var authorId = Guid.NewGuid();
        using var convert = await PostWithKeyAsync(
            client, HttpMethod.Post,
            $"/api/v1/leads/{leadId}/convert?companyId={ErpApiFactory.DevCompanyId}",
            new
            {
                customerCode,
                defaultCurrency = "USD",
                paymentTermsDays = 30,
                opportunityAmount = 15000m,
                opportunityProbability = 25m,
                expectedClosingDate = "2027-06-30",
                convertedByUserId = authorId,
            });
        Assert.Equal(HttpStatusCode.OK, convert.StatusCode);
        var converted = JsonNode.Parse(await convert.Content.ReadAsStringAsync())!;
        var opportunityId = converted["opportunityId"]!.GetValue<Guid>();
        _opportunityIds.Add(opportunityId);
        _customerIds.Add(converted["customerId"]!.GetValue<Guid>());
        Assert.Equal(leadId, converted["leadId"]!.GetValue<Guid>());
        Assert.Equal(customerCode, converted["customerCode"]!.GetValue<string>());
        Assert.Equal(3750m, converted["weightedPipelineAmount"]!.GetValue<decimal>());

        // Lead flipped, deal open at 25%, conversion note copied onto the deal (CRM-03 trail).
        Assert.Equal("Converted", await ReadLeadStatusAsync(leadId));
        var deal = await ReadOpportunityAsync(opportunityId);
        Assert.Equal("Open", deal.Status);
        Assert.Equal(25m, deal.Probability);
        Assert.Equal(15000m, deal.Amount);
        Assert.True(await HasConversionNoteAsync(opportunityId, leadCode));

        // Proposal with no explicit probability syncs the 50% milestone: weighted 7500.
        using var toProposal = await PostAdvanceAsync(client, opportunityId, "Proposal");
        Assert.Equal(HttpStatusCode.OK, toProposal.StatusCode);
        var proposal = JsonNode.Parse(await toProposal.Content.ReadAsStringAsync())!;
        Assert.Equal(50m, proposal["probability"]!.GetValue<decimal>());
        Assert.Equal(7500m, proposal["weightedAmount"]!.GetValue<decimal>());

        // Negotiation syncs 80%: weighted 12000.
        using var toNegotiation = await PostAdvanceAsync(client, opportunityId, "Negotiation");
        Assert.Equal(HttpStatusCode.OK, toNegotiation.StatusCode);
        var negotiation = JsonNode.Parse(await toNegotiation.Content.ReadAsStringAsync())!;
        Assert.Equal(80m, negotiation["probability"]!.GetValue<decimal>());
        Assert.Equal(12000m, negotiation["weightedAmount"]!.GetValue<decimal>());

        // ClosedWon forces 100%: weighted == amount (CRM-01 terminal math).
        using var toWon = await PostAdvanceAsync(client, opportunityId, "ClosedWon");
        Assert.Equal(HttpStatusCode.OK, toWon.StatusCode);
        var won = JsonNode.Parse(await toWon.Content.ReadAsStringAsync())!;
        Assert.Equal("Won", won["status"]!.GetValue<string>());
        Assert.Equal(100m, won["probability"]!.GetValue<decimal>());
        Assert.Equal(15000m, won["weightedAmount"]!.GetValue<decimal>());

        deal = await ReadOpportunityAsync(opportunityId);
        Assert.Equal(("ClosedWon", "Won", 100m, 15000m), (deal.Stage, deal.Status, deal.Probability, deal.Amount));
    }

    // ------------------------------------------------------------- CRM-04 replay live

    /// <summary>
    /// CRM-04 live, both guards: replaying the SAME <c>Idempotency-Key</c> returns 200 with the
    /// original body byte-identical and zero new lead rows (HTTP filter); re-sending the same
    /// <c>DeduplicationKey</c> triple under a DIFFERENT key (and even a different lead code)
    /// returns 200 with <c>duplicate=true</c>, the ORIGINAL lead id, and zero new rows
    /// (body-level dedup).
    /// </summary>
    [Fact]
    public async Task IngestReplay_SameKeyTwiceThenSameDedupKey_SecondIs200IdenticalWithZeroNewRows()
    {
        using var client = CreateClient();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var leadCode = $"CRM-IT-{tag}-L1";
        var dedupKey = $"crm-it-{tag}-web-9";

        var payload = IngestPayload(leadCode, dedupKey, "Replay Rita");
        var key = Guid.NewGuid().ToString("N");
        using var first = await PostWithKeyAsync(client, HttpMethod.Post, "/api/v1/leads/ingest", payload, key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstBytes = await first.Content.ReadAsByteArrayAsync();
        var leadId = JsonNode.Parse(Encoding.UTF8.GetString(firstBytes))!["lead"]!["id"]!.GetValue<Guid>();
        _leadIds.Add(leadId);
        Assert.Equal(1, await CountLeadsByCodeAsync(leadCode));

        // Same HTTP key, same body: the filter replays the stored 201 body verbatim as a 200.
        using var replay = await PostWithKeyAsync(client, HttpMethod.Post, "/api/v1/leads/ingest", payload, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(firstBytes, await replay.Content.ReadAsByteArrayAsync());
        Assert.Equal(leadId, JsonNode.Parse(Encoding.UTF8.GetString(firstBytes))!["lead"]!["id"]!.GetValue<Guid>());
        Assert.Equal(1, await CountLeadsByCodeAsync(leadCode));

        // Same dedup triple, DIFFERENT http key and DIFFERENT lead code: the handler - not the
        // filter - answers duplicate=true with the original lead and writes nothing.
        using var dedupReplay = await PostWithKeyAsync(
            client, HttpMethod.Post, "/api/v1/leads/ingest",
            IngestPayload($"CRM-IT-{tag}-L2", dedupKey, "Replay Rita Renamed"));
        Assert.Equal(HttpStatusCode.OK, dedupReplay.StatusCode);
        var dedupBody = JsonNode.Parse(await dedupReplay.Content.ReadAsStringAsync())!;
        Assert.True(dedupBody["duplicate"]!.GetValue<bool>());
        Assert.Equal(leadId, dedupBody["lead"]!["id"]!.GetValue<Guid>());
        Assert.Equal(leadCode, dedupBody["lead"]!["leadCode"]!.GetValue<string>());
        Assert.Equal(1, await CountLeadsByDedupKeyAsync(dedupKey));
        Assert.Equal(0, await CountLeadsByCodeAsync($"CRM-IT-{tag}-L2"));
    }

    // ------------------------------------------------- CRM-02/CRM-03 close-rules live

    /// <summary>
    /// Close-rules live: ClosedLost without a reason is a 400
    /// <c>crm_loss_reason_required</c> with the deal still Open; with a reason it closes at
    /// 0% (weighted 0); any further advance is a 409 with the row untouched.
    /// </summary>
    [Fact]
    public async Task CloseRules_LostWithoutReasonIs400ThenLostThenDoubleCloseIs409()
    {
        using var client = CreateClient();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var (leadId, opportunityId, customerId) = await SetupConvertedDealAsync(client, tag, 50000m, 80m);
        _leadIds.Add(leadId);
        _opportunityIds.Add(opportunityId);
        _customerIds.Add(customerId);

        // No reason: 400, deal untouched.
        using var noReason = await PostAdvanceAsync(client, opportunityId, "ClosedLost");
        var problem = await AssertProblemAsync(
            noReason, HttpStatusCode.BadRequest, "Opportunity Rejected", "crm_loss_reason_required");
        Assert.Contains("Loss Reason", problem["detail"]!.GetValue<string>());
        var deal = await ReadOpportunityAsync(opportunityId);
        Assert.Equal("Open", deal.Status);

        // With reason: Lost at 0%, weighted 0 (CRM-01 terminal math).
        using var lost = await PostAdvanceAsync(client, opportunityId, "ClosedLost", LossReason: "Competitor priced 15% lower");
        Assert.Equal(HttpStatusCode.OK, lost.StatusCode);
        var lostBody = JsonNode.Parse(await lost.Content.ReadAsStringAsync())!;
        Assert.Equal("Lost", lostBody["status"]!.GetValue<string>());
        Assert.Equal(0m, lostBody["probability"]!.GetValue<decimal>());
        Assert.Equal(0m, lostBody["weightedAmount"]!.GetValue<decimal>());
        Assert.Equal("Competitor priced 15% lower", lostBody["lossReason"]!.GetValue<string>());

        // Double-close: 409, zero further writes.
        using var doubleClose = await PostAdvanceAsync(client, opportunityId, "ClosedWon");
        await AssertProblemAsync(
            doubleClose, HttpStatusCode.Conflict, "Opportunity Conflict", "invalid_status_transition");
        deal = await ReadOpportunityAsync(opportunityId);
        Assert.Equal(("ClosedLost", "Lost", 0m), (deal.Stage, deal.Status, deal.Probability));
    }

    /// <summary>
    /// Spec 00-i18n F-17: the same close-rule rejection under <c>Accept-Language: es</c> carries
    /// the Spanish title and detail while the machine code stays invariant. Lives here (not in
    /// <c>ErrorLocalizationApiTests</c>) because reaching the rule requires a real converted deal.
    /// </summary>
    [Fact]
    public async Task CloseRules_LostWithoutReason_WithEsHeader_ReturnsSpanishProblemDetails()
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Add("Accept-Language", "es");
        var tag = Guid.NewGuid().ToString("N")[..8];
        var (leadId, opportunityId, customerId) = await SetupConvertedDealAsync(client, tag, 50000m, 80m);
        _leadIds.Add(leadId);
        _opportunityIds.Add(opportunityId);
        _customerIds.Add(customerId);

        using var noReason = await PostAdvanceAsync(client, opportunityId, "ClosedLost");
        var problem = await AssertProblemAsync(
            noReason, HttpStatusCode.BadRequest, "Oportunidad rechazada", "crm_loss_reason_required");
        Assert.Equal(
            "Se requiere un motivo de pérdida al cerrar una oportunidad como perdida.",
            problem["detail"]!.GetValue<string>());
    }

    // ------------------------------------------------------- CRM-05 re-open live

    /// <summary>
    /// CRM-05 live: a ClosedLost deal re-opens to Negotiation/50%/Open with the loss reason
    /// cleared while the conversion history (activities) stays attached; the board read shows
    /// the re-opened stage.
    /// </summary>
    [Fact]
    public async Task Reopen_LostDeal_ReturnsToNegotiationWithHistoryIntact()
    {
        using var client = CreateClient();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var (leadId, opportunityId, customerId) = await SetupConvertedDealAsync(client, tag, 20000m, 25m);
        _leadIds.Add(leadId);
        _opportunityIds.Add(opportunityId);
        _customerIds.Add(customerId);

        using var lost = await PostAdvanceAsync(client, opportunityId, "ClosedLost", LossReason: "Budget frozen Q1");
        Assert.Equal(HttpStatusCode.OK, lost.StatusCode);
        var activitiesBefore = await CountActivitiesAsync(opportunityId);
        Assert.True(activitiesBefore >= 1);

        using var reopen = await PostWithKeyAsync(
            client, HttpMethod.Post,
            $"/api/v1/opportunities/{opportunityId}/reopen?companyId={ErpApiFactory.DevCompanyId}&newProbability=50",
            new { });
        Assert.Equal(HttpStatusCode.OK, reopen.StatusCode);
        Assert.Equal(opportunityId, JsonNode.Parse(await reopen.Content.ReadAsStringAsync())!.GetValue<Guid>());

        var deal = await ReadOpportunityAsync(opportunityId);
        Assert.Equal(("Negotiation", "Open", 50m), (deal.Stage, deal.Status, deal.Probability));
        Assert.Null(deal.LossReason);
        Assert.Equal(activitiesBefore, await CountActivitiesAsync(opportunityId));

        // The pipeline board read reflects the re-opened stage.
        using var board = await client.GetAsync(
            $"/api/v1/opportunities?companyId={ErpApiFactory.DevCompanyId}&limit=50");
        Assert.Equal(HttpStatusCode.OK, board.StatusCode);
        var row = JsonNode.Parse(await board.Content.ReadAsStringAsync())!.AsArray()
            .Single(n => n!["id"]!.GetValue<Guid>() == opportunityId);
        Assert.Equal("Negotiation", row!["stage"]!.GetValue<string>());

        // Fix-pass W3 (temporal-history proof, zero production-code change): the pre-reopen
        // Lost row with its loss reason survives in OpportunityHistory - spec CRM-05 "loss
        // reason preserved in audit history".
        Assert.True(await HasLostHistoryRowAsync(opportunityId, "Budget frozen Q1"));
    }

    // ------------------------------------------------------- CRM-06 race live

    /// <summary>
    /// CRM-06 live: two concurrent advances carrying the SAME <c>RowVersion</c> resolve to
    /// exactly one winner (200); the loser is a 409 <c>concurrency_conflict</c>. The row ends
    /// in exactly one of the two target stages - zero partial writes.
    /// </summary>
    [Fact]
    public async Task ConcurrentAdvances_SameRowVersion_ExactlyOneWinsWithConcurrencyConflict()
    {
        using var client = CreateClient();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var (leadId, opportunityId, customerId) = await SetupConvertedDealAsync(client, tag, 30000m, 10m);
        _leadIds.Add(leadId);
        _opportunityIds.Add(opportunityId);
        _customerIds.Add(customerId);

        var rowVersion = Uri.EscapeDataString((await ReadOpportunityDtoAsync(client, opportunityId))["rowVersion"]!.GetValue<string>());
        Assert.NotEqual(string.Empty, Uri.UnescapeDataString(rowVersion));

        // TRULY concurrent: both advances race on the same token with distinct idempotency
        // keys (a shared key would replay instead of racing). The RowVersion WHERE clause
        // serializes them: first commit wins, second matches zero rows.
        var racerOne = Task.Run(() => PostAdvanceAsync(client, opportunityId, "Qualification", rowVersion));
        var racerTwo = Task.Run(() => PostAdvanceAsync(client, opportunityId, "Proposal", rowVersion));
        using var first = await racerOne;
        using var second = await racerTwo;

        var outcomes = new[] { first, second };
        var winner = Assert.Single(outcomes, r => r.StatusCode == HttpStatusCode.OK);
        var loser = Assert.Single(outcomes, r => r.StatusCode != HttpStatusCode.OK);
        await AssertProblemAsync(
            loser, HttpStatusCode.Conflict, "Opportunity Conflict", "concurrency_conflict");

        var winnerStage = JsonNode.Parse(await winner.Content.ReadAsStringAsync())!["stage"]!.GetValue<string>();
        Assert.Contains(winnerStage, new[] { "Qualification", "Proposal" });
        var deal = await ReadOpportunityAsync(opportunityId);
        Assert.Equal(winnerStage, deal.Stage);
        Assert.Equal("Open", deal.Status);
    }

    // --------------------------------- CRM-02 1-click sales-order creation live

    /// <summary>
    /// Fix-pass C2 live: a ClosedWon deal converts to a formal sales order in ONE call -
    /// 201 with the gapless SO number, Draft status and server-side totals equal to the
    /// opportunity amount (single line: quantity 1 at the deal amount) - while the linkage
    /// note lands on the deal (SalesOrder carries no OpportunityId FK) and the converted
    /// customer owns exactly one order row.
    /// </summary>
    [Fact]
    public async Task CreateSalesOrder_WonDeal_Returns201DraftOrderLinkedByNote()
    {
        using var client = CreateClient();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var (leadId, opportunityId, customerId) = await SetupConvertedDealAsync(client, tag, 15000m, 25m);
        _leadIds.Add(leadId);
        _opportunityIds.Add(opportunityId);
        _customerIds.Add(customerId);

        using var toWon = await PostAdvanceAsync(client, opportunityId, "ClosedWon");
        Assert.Equal(HttpStatusCode.OK, toWon.StatusCode);

        using var created = await PostWithKeyAsync(
            client, HttpMethod.Post,
            $"/api/v1/opportunities/{opportunityId}/create-sales-order?companyId={ErpApiFactory.DevCompanyId}",
            new
            {
                itemId = SeededItemId,
                quantity = 1m,
                rate = 15000m,
                createdByUserId = Guid.NewGuid(),
            });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);

        var order = JsonNode.Parse(await created.Content.ReadAsStringAsync())!;
        var orderId = order["id"]!.GetValue<Guid>();
        _salesOrderIds.Add(orderId);
        Assert.Matches(@"^SO-\d{4}-\d{5}$", order["orderNumber"]!.GetValue<string>());
        Assert.Equal("Draft", order["status"]!.GetValue<string>());
        Assert.Equal(customerId, order["customerId"]!.GetValue<Guid>());
        Assert.Equal(15000m, order["grandTotal"]!.GetValue<decimal>());
        var line = Assert.Single(order["lines"]!.AsArray());
        Assert.Equal((SeededItemId, 1m, 15000m), (
            line!["itemId"]!.GetValue<Guid>(),
            line["quantity"]!.GetValue<decimal>(),
            line["rate"]!.GetValue<decimal>()));

        // Delta oracle: the converted customer owns exactly this one order row.
        Assert.Equal(1, await CountSalesOrdersAsync(customerId));

        // Linkage proof: SalesOrder has no OpportunityId FK, so the deal carries a note
        // naming the created order number (the CRM-03 conversion-note precedent).
        Assert.True(await HasSalesOrderNoteAsync(
            opportunityId, order["orderNumber"]!.GetValue<string>()));
    }

    /// <summary>
    /// C2 guard live: an OPEN deal cannot mint a sales order - 409
    /// <c>crm_opportunity_not_won</c> with zero new order rows (zero-write proof).
    /// </summary>
    [Fact]
    public async Task CreateSalesOrder_OpenDeal_Is409WithZeroNewOrders()
    {
        using var client = CreateClient();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var (leadId, opportunityId, customerId) = await SetupConvertedDealAsync(client, tag, 15000m, 25m);
        _leadIds.Add(leadId);
        _opportunityIds.Add(opportunityId);
        _customerIds.Add(customerId);

        using var rejected = await PostWithKeyAsync(
            client, HttpMethod.Post,
            $"/api/v1/opportunities/{opportunityId}/create-sales-order?companyId={ErpApiFactory.DevCompanyId}",
            new { itemId = SeededItemId, quantity = 1m, rate = 15000m });
        await AssertProblemAsync(
            rejected, HttpStatusCode.Conflict, "Opportunity Conflict", "crm_opportunity_not_won");
        Assert.Equal(0, await CountSalesOrdersAsync(customerId));
    }

    // ------------------------------------------------- W1 concurrent-ingest race live

    /// <summary>
    /// Fix-pass W1 live: two TRULY concurrent ingests carrying the SAME
    /// <c>(CompanyId, Source, DeduplicationKey)</c> triple (distinct lead codes AND distinct
    /// idempotency keys, so neither the HTTP filter nor the pre-check can serialize them)
    /// resolve to exactly one 201 plus one 200 <c>duplicate=true</c> naming the SAME lead id
    /// - and the filtered unique index <c>UQ_Lead_Company_Source_ExternalRef</c> guarantees
    /// exactly one lead row (the loser re-reads the winner, zero new rows).
    /// </summary>
    [Fact]
    public async Task ConcurrentIngests_SameDedupTriple_ExactlyOneWinsWithDuplicateTrue()
    {
        using var client = CreateClient();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var dedupKey = $"crm-it-{tag}-race-1";

        var racerOne = Task.Run(() => PostIngestAsync(client, $"CRM-IT-{tag}-R1", dedupKey, "Race Ana"));
        var racerTwo = Task.Run(() => PostIngestAsync(client, $"CRM-IT-{tag}-R2", dedupKey, "Race Ana Again"));
        using var first = await racerOne;
        using var second = await racerTwo;

        var outcomes = new[] { first, second };
        var winner = Assert.Single(outcomes, r => r.StatusCode == HttpStatusCode.Created);
        var loser = Assert.Single(outcomes, r => r.StatusCode == HttpStatusCode.OK);

        var winnerBody = JsonNode.Parse(await winner.Content.ReadAsStringAsync())!;
        var loserBody = JsonNode.Parse(await loser.Content.ReadAsStringAsync())!;
        Assert.False(winnerBody["duplicate"]!.GetValue<bool>());
        Assert.True(loserBody["duplicate"]!.GetValue<bool>());
        var winnerId = winnerBody["lead"]!["id"]!.GetValue<Guid>();
        Assert.Equal(winnerId, loserBody["lead"]!["id"]!.GetValue<Guid>());
        _leadIds.Add(winnerId);

        // Exactly one lead row for the triple - no double insert, no lost update.
        Assert.Equal(1, await CountLeadsByDedupKeyAsync(dedupKey));
    }

    // ------------------------------------------------- S3 reopen-probability guard live

    /// <summary>
    /// Fix-pass S3 live: reopening with an out-of-range probability is a 400
    /// <c>crm_invalid_probability_range</c> (the advance guard mirrored) and the Lost deal
    /// stays untouched - zero writes.
    /// </summary>
    [Fact]
    public async Task Reopen_OutOfRangeProbability_Is400WithZeroWrites()
    {
        using var client = CreateClient();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var (leadId, opportunityId, customerId) = await SetupConvertedDealAsync(client, tag, 20000m, 25m);
        _leadIds.Add(leadId);
        _opportunityIds.Add(opportunityId);
        _customerIds.Add(customerId);

        using var lost = await PostAdvanceAsync(client, opportunityId, "ClosedLost", LossReason: "Budget frozen Q1");
        Assert.Equal(HttpStatusCode.OK, lost.StatusCode);

        using var rejected = await PostWithKeyAsync(
            client, HttpMethod.Post,
            $"/api/v1/opportunities/{opportunityId}/reopen?companyId={ErpApiFactory.DevCompanyId}&newProbability=150",
            new { });
        await AssertProblemAsync(
            rejected, HttpStatusCode.BadRequest, "Opportunity Rejected", "crm_invalid_probability_range");

        var deal = await ReadOpportunityAsync(opportunityId);
        Assert.Equal(("ClosedLost", "Lost", 0m), (deal.Stage, deal.Status, deal.Probability));
        Assert.Equal("Budget frozen Q1", deal.LossReason);
    }

    // ---------------------------------------------------------------------------------- helpers

    /// <summary>Tenant-scoped client: only X-Tenant-ID (see ErpApiFactory remarks - auth is NOT disabled).</summary>
    private HttpClient CreateClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-ID", ErpApiFactory.DevTenantId.ToString());
        return client;
    }

    /// <summary>POSTs one JSON body with a FRESH Idempotency-Key when none is given (Constitution VI.4).</summary>
    private static Task<HttpResponseMessage> PostWithKeyAsync(
        HttpClient client, HttpMethod method, string url, object? payload = null, string? key = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        request.Headers.Add(IdempotencyKeyHeader, key ?? Guid.NewGuid().ToString("N"));
        return client.SendAsync(request);
    }

    private static object IngestPayload(string leadCode, string dedupKey, string leadName) => new
    {
        companyId = ErpApiFactory.DevCompanyId,
        leadCode,
        leadName,
        organizationName = "TechCorp",
        email = "alex.rivera@techcorp.example",
        phone = "+1-555-0100",
        source = "Website",
        deduplicationKey = dedupKey,
    };

    private static Task<HttpResponseMessage> PostIngestAsync(HttpClient client, string leadCode, string dedupKey, string leadName) =>
        PostWithKeyAsync(client, HttpMethod.Post, "/api/v1/leads/ingest", IngestPayload(leadCode, dedupKey, leadName));

    private static Task<HttpResponseMessage> PostAdvanceAsync(
        HttpClient client, Guid opportunityId, string toStage, string? rowVersion = null, string? LossReason = null)
    {
        var url = $"/api/v1/opportunities/{opportunityId}/advance?companyId={ErpApiFactory.DevCompanyId}"
            + (rowVersion is null ? string.Empty : $"&rowVersion={rowVersion}");
        return PostWithKeyAsync(client, HttpMethod.Post, url, new { toStage, lossReason = LossReason });
    }

    /// <summary>
    /// Ingests one lead and converts it (tracks nothing - the caller owns the ids), so the
    /// close/reopen/race tests start from a live open deal without duplicating the flow.
    /// </summary>
    private static async Task<(Guid LeadId, Guid OpportunityId, Guid CustomerId)> SetupConvertedDealAsync(
        HttpClient client, string tag, decimal amount, decimal probability)
    {
        var suffix = Guid.NewGuid().ToString("N")[..4];
        using var ingest = await PostIngestAsync(client, $"CRM-IT-{tag}-{suffix}-L1", $"crm-it-{tag}-{suffix}-web-1", "Casey Close");
        Assert.Equal(HttpStatusCode.Created, ingest.StatusCode);
        var leadId = JsonNode.Parse(await ingest.Content.ReadAsStringAsync())!["lead"]!["id"]!.GetValue<Guid>();

        using var convert = await PostWithKeyAsync(
            client, HttpMethod.Post,
            $"/api/v1/leads/{leadId}/convert?companyId={ErpApiFactory.DevCompanyId}",
            new
            {
                customerCode = $"CRM-IT-{tag}-{suffix}-C1",
                defaultCurrency = "USD",
                paymentTermsDays = 30,
                opportunityAmount = amount,
                opportunityProbability = probability,
                expectedClosingDate = "2027-06-30",
                convertedByUserId = Guid.NewGuid(),
            });
        Assert.Equal(HttpStatusCode.OK, convert.StatusCode);
        var converted = JsonNode.Parse(await convert.Content.ReadAsStringAsync())!;
        return (leadId, converted["opportunityId"]!.GetValue<Guid>(), converted["customerId"]!.GetValue<Guid>());
    }

    private static async Task<JsonNode> ReadOpportunityDtoAsync(HttpClient client, Guid opportunityId)
    {
        using var board = await client.GetAsync(
            $"/api/v1/opportunities?companyId={ErpApiFactory.DevCompanyId}&limit=50");
        Assert.Equal(HttpStatusCode.OK, board.StatusCode);
        return JsonNode.Parse(await board.Content.ReadAsStringAsync())!.AsArray()
            .Single(n => n!["id"]!.GetValue<Guid>() == opportunityId)!;
    }

    /// <summary>
    /// Asserts the RFC 7807 contract of a rejection: status line, <c>status</c> extension, title
    /// and the stable machine code in the <c>code</c> extension (the CustomersApiTests shape).
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

    // ----------------------------------------------------------------------------- SQL oracles

    private static async Task<T> QuerySingleAsync<T>(string sql, params (string Name, object? Value)[] parameters)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        return (T)(await command.ExecuteScalarAsync())!;
    }

    private static Task<string> ReadLeadStatusAsync(Guid leadId) =>
        QuerySingleAsync<string>(
            "SELECT Status FROM dbo.Lead WHERE Id = @Id AND TenantId = @TenantId;",
            ("@Id", leadId),
            ("@TenantId", ErpApiFactory.DevTenantId));

    private sealed record DealRow(string Stage, string Status, decimal Amount, decimal Probability, string? LossReason);

    private static async Task<DealRow> ReadOpportunityAsync(Guid opportunityId)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT Stage, Status, OpportunityAmount, Probability, LossReason FROM dbo.Opportunity "
            + "WHERE Id = @Id AND TenantId = @TenantId;", connection);
        command.Parameters.AddWithValue("@Id", opportunityId);
        command.Parameters.AddWithValue("@TenantId", ErpApiFactory.DevTenantId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new DealRow(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetDecimal(2),
            reader.GetDecimal(3),
            await reader.IsDBNullAsync(4) ? null : reader.GetString(4));
    }

    private static async Task<int> CountLeadsByCodeAsync(string leadCode)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT COUNT_BIG(*) FROM dbo.Lead WHERE TenantId = @TenantId AND LeadCode = @Code;",
            connection);
        command.Parameters.AddWithValue("@TenantId", ErpApiFactory.DevTenantId);
        command.Parameters.AddWithValue("@Code", leadCode);
        return checked((int)(long)(await command.ExecuteScalarAsync())!);
    }

    private static async Task<int> CountLeadsByDedupKeyAsync(string dedupKey)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT COUNT_BIG(*) FROM dbo.Lead WHERE TenantId = @TenantId AND ExternalReference = @Key;",
            connection);
        command.Parameters.AddWithValue("@TenantId", ErpApiFactory.DevTenantId);
        command.Parameters.AddWithValue("@Key", dedupKey);
        return checked((int)(long)(await command.ExecuteScalarAsync())!);
    }

    private static async Task<int> CountActivitiesAsync(Guid opportunityId)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT COUNT_BIG(*) FROM dbo.CRMActivity WHERE OpportunityId = @Id;", connection);
        command.Parameters.AddWithValue("@Id", opportunityId);
        return checked((int)(long)(await command.ExecuteScalarAsync())!);
    }

    private static async Task<bool> HasConversionNoteAsync(Guid opportunityId, string leadCode)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT COUNT_BIG(*) FROM dbo.CRMActivity WHERE OpportunityId = @Id AND Content LIKE @Pattern;",
            connection);
        command.Parameters.AddWithValue("@Id", opportunityId);
        command.Parameters.AddWithValue("@Pattern", $"%{leadCode}%");
        return (long)(await command.ExecuteScalarAsync())! > 0;
    }

    private static async Task<int> CountSalesOrdersAsync(Guid customerId)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT COUNT_BIG(*) FROM dbo.SalesOrder WHERE TenantId = @TenantId AND CustomerId = @CustomerId;",
            connection);
        command.Parameters.AddWithValue("@TenantId", ErpApiFactory.DevTenantId);
        command.Parameters.AddWithValue("@CustomerId", customerId);
        return checked((int)(long)(await command.ExecuteScalarAsync())!);
    }

    private static async Task<bool> HasSalesOrderNoteAsync(Guid opportunityId, string orderNumber)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT COUNT_BIG(*) FROM dbo.CRMActivity WHERE OpportunityId = @Id AND Subject LIKE @Pattern;",
            connection);
        command.Parameters.AddWithValue("@Id", opportunityId);
        command.Parameters.AddWithValue("@Pattern", $"%{orderNumber}%");
        return (long)(await command.ExecuteScalarAsync())! > 0;
    }

    /// <summary>
    /// Fix-pass W3 temporal proof: the pre-reopen Lost version of the deal - with its loss
    /// reason - is preserved in the system-versioned history table (Opportunity is temporal,
    /// plan.md §1), so the CRM-05 audit requirement holds with zero production-code change.
    /// </summary>
    private static async Task<bool> HasLostHistoryRowAsync(Guid opportunityId, string lossReason)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT COUNT_BIG(*) FROM dbo.OpportunityHistory "
            + "WHERE Id = @Id AND Status = N'Lost' AND LossReason = @Reason;",
            connection);
        command.Parameters.AddWithValue("@Id", opportunityId);
        command.Parameters.AddWithValue("@Reason", lossReason);
        return (long)(await command.ExecuteScalarAsync())! > 0;
    }

    /// <summary>
    /// Removes every per-test row in FK order (activities + order lines -&gt; orders -&gt;
    /// opportunities -&gt; leads -&gt; customers). Idempotency reservations stay: they key
    /// unique per-run GUIDs and no other class reads them.
    /// </summary>
    public void Dispose()
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        connection.Open();

        foreach (var (sql, ids) in new (string, List<Guid>)[]
        {
            ("DELETE FROM dbo.CRMActivity WHERE OpportunityId IN (SELECT value FROM OPENJSON(@Ids));", _opportunityIds),
            ("DELETE FROM dbo.SalesOrderItem WHERE SalesOrderId IN (SELECT value FROM OPENJSON(@Ids));", _salesOrderIds),
            ("DELETE FROM dbo.SalesOrder WHERE Id IN (SELECT value FROM OPENJSON(@Ids));", _salesOrderIds),
            ("DELETE FROM dbo.Opportunity WHERE Id IN (SELECT value FROM OPENJSON(@Ids));", _opportunityIds),
            ("DELETE FROM dbo.Lead WHERE Id IN (SELECT value FROM OPENJSON(@Ids));", _leadIds),
            ("DELETE FROM dbo.Customer WHERE Id IN (SELECT value FROM OPENJSON(@Ids));", _customerIds),
        })
        {
            if (ids.Count == 0)
            {
                continue;
            }

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue(
                "@Ids", System.Text.Json.JsonSerializer.Serialize(ids.Select(id => id.ToString())));
            command.ExecuteNonQuery();
        }
    }
}
