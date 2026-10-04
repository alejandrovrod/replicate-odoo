using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Erp.Api.IntegrationTests;

/// <summary>
/// tasks.md 2.5 end-to-end evidence for the four Financial Reporting queries against the LIVE dev
/// container: the pinned Task 2.6 general-ledger contract, the acceptance scenario
/// ("Trial balance reports zero discrepancy"), the balance sheet's accounting equation and the
/// P&amp;L formula, plus the three RFC 7807 400 codes the endpoints own.
/// </summary>
/// <remarks>
/// <para><b>The oracle is the DATABASE, not only the response.</b> A 200 only proves the API
/// answered; these tests make statements about what the REPORT says the ledger holds, so every
/// number is recomputed straight from <c>dbo.GLEntry</c> with raw SQL. The SQL is written from the
/// accounting identities themselves (natural signs, Dr − Cr nets), NOT by mirroring the C# handler,
/// so a bug in the handler cannot agree with itself.</para>
///
/// <para><b>Serialized with the other ledger writers through <see cref="LedgerMutatingCollection"/>
/// </b> on purpose: these tests read GLOBAL aggregates (whole-company sums), so a voucher another
/// class appends between the HTTP response and the SQL oracle would make them flaky. Joining the
/// collection means every ledger append in the assembly happens one test at a time.</para>
///
/// <para><b>Known side effect:</b> the acceptance test submits one balanced voucher (350.00 Dr /
/// 350.00 Cr) that LEAVES its rows behind - the append-only law (Constitution III.2) forbids
/// cleaning them up, and a zero-sum voucher can never skew a later assertion anyway.</para>
///
/// <para><b>Cutoffs are 2026-12-31</b> so every statement sees the whole seeded ledger
/// (2026-03-15 .. 2026-10-03) plus whatever the integration suite posted, and the SQL oracle always
/// uses the SAME cutoff as the request - agreement is by construction, not by a hardcoded total
/// that would rot the next time somebody seeds data.</para>
/// </remarks>
[Collection(LedgerMutatingCollection.Name)]
public class FinancialReportsApiTests : IClassFixture<ErpApiFactory>
{
    /// <summary>Account 1110 "Cash and Cash Equivalents" - active leaf of the dev company.</summary>
    private static readonly Guid CashAccount =
        Guid.Parse("a0000000-0000-4000-8000-000000001110");

    /// <summary>Account 5110 "Office Supplies Expense" - active leaf of the dev company.</summary>
    private static readonly Guid OfficeSuppliesAccount =
        Guid.Parse("a0000000-0000-4000-8000-000000005110");

    /// <summary>Cutoff used by every "everything so far" statement in this class.</summary>
    private static readonly DateOnly EverythingCutoff = new(2026, 12, 31);

    /// <summary>Earliest bound a P&amp;L can ask for - well before the first seeded posting.</summary>
    private const string EpochFrom = "2000-01-01";

    private readonly ErpApiFactory _factory;

    public FinancialReportsApiTests(ErpApiFactory factory) => _factory = factory;

    // ---------------------------------------------------------------------------------------------
    // GET general-ledger - the PINNED Task 2.6 wire contract
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The frozen contract the parallel Task 2.6 agent is coding against, proved byte-for-byte on
    /// the wire: exactly the four report fields and exactly the eighteen entry fields the React
    /// viewer reads (no renames, no extras), the first page row is genuinely the OLDEST ledger row,
    /// and <c>totalDebit</c>/<c>totalCredit</c> describe the FULL 57+ row set even though
    /// <c>take=1</c> returned a single item.
    /// </summary>
    [Fact]
    public async Task GeneralLedger_ReturnsPinnedShape_OldestRowAndFullSetTotals()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(
            $"/api/v1/FinancialReports/general-ledger?companyId={ErpApiFactory.DevCompanyId}&take=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var report = (JsonObject)JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

        // The report envelope itself is frozen: items + the three totals, nothing else.
        AssertExactKeys(report, "items", "totalDebit", "totalCredit", "difference");

        var items = report["items"]!.AsArray();
        Assert.Single(items); // take=1 is a LIMIT, not a contract change
        var entry = (JsonObject)items[0]!;

        // The eighteen pinned entry fields - the exact list GeneralLedgerOverview.tsx consumes.
        // Asserting the COUNT too means a renamed OR a silently added field fails the build gate.
        AssertExactKeys(
            entry,
            "id",
            "postingDate",
            "accountId",
            "accountCode",
            "accountName",
            "rootType",
            "debit",
            "credit",
            "accountCurrency",
            "voucherType",
            "voucherNo",
            "voucherId",
            "partyType",
            "partyId",
            "costCenterId",
            "isCancelled",
            "remarks",
            "createdAt");

        // Types the frontend depends on: a long id, a bare yyyy-MM-dd date (NOT an ISO timestamp),
        // an enum rendered as its name, and an ISO-8601 instant for when the row was appended.
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", entry["postingDate"]!.GetValue<string>());
        Assert.Matches(@"^\d+$", entry["id"]!.ToJsonString().Trim('"'));
        Assert.True(
            DateTimeOffset.TryParse(entry["createdAt"]!.GetValue<string>(), out _),
            "createdAt must be an ISO-8601 timestamp.");

        // Ordering: PostingDate ASC then Id ASC, so the FIRST row of the report is the first row
        // of the ledger itself - the drill-down a reviewer starts from.
        var first = await ReadFirstLedgerRowAsync();
        Assert.Equal(first.Id, entry["id"]!.GetValue<long>());
        Assert.Equal(
            first.PostingDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            entry["postingDate"]!.GetValue<string>());

        // THE pinned behaviour: totals cover the FULL filtered set while the page is truncated.
        var oracle = await ReadTotalsAsync(EverythingCutoff);
        Assert.Equal(oracle.TotalDebit, report["totalDebit"]!.GetValue<decimal>());
        Assert.Equal(oracle.TotalCredit, report["totalCredit"]!.GetValue<decimal>());
        Assert.Equal(
            oracle.TotalDebit - oracle.TotalCredit,
            report["difference"]!.GetValue<decimal>());

        // Proof the totals are NOT the page's sums: with more than one ledger row in existence a
        // single-item page cannot reproduce the whole-company total.
        if (oracle.RowCount > 1)
        {
            var pageDebit = items.Sum(item => item!["debit"]!.GetValue<decimal>());
            Assert.NotEqual(pageDebit, report["totalDebit"]!.GetValue<decimal>());
        }
    }

    // ---------------------------------------------------------------------------------------------
    // GET trial-balance - tasks.md 2.5 ACCEPTANCE
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// THE acceptance of tasks.md 2.5: submit the accountant's balanced voucher (debit 5110 for
    /// $350.00, credit 1110 for $350.00) through <c>POST /api/v1/journal-entries</c> + the submit
    /// transition, then read the trial balance and find <c>difference == 0.0000</c> - plus a
    /// per-account cross-check against <c>dbo.GLEntry</c> so "zero" is proven to come from real
    /// aggregates rather than from an empty or mis-scoped report.
    /// </summary>
    [Fact]
    public async Task TrialBalance_AfterSubmittingBalancedVoucher_ReportsZeroDiscrepancy()
    {
        using var client = CreateClient();

        // A voucher posted through the REAL pipeline: drafts never touch the ledger (AC-02 relies
        // on that), so nothing below can pass for the wrong reason.
        var draft = await CreateDraftAsync(
            client,
            new DateOnly(2026, 3, 15),
            "Task 2.5 acceptance: balanced voucher for the trial balance",
            (OfficeSuppliesAccount, 350m, 0m),
            (CashAccount, 0m, 350m));

        var voucherId = draft["id"]!.GetValue<Guid>();
        Assert.Equal("Draft", draft["status"]!.GetValue<string>());

        using (var submitResponse = await PostEmptyBodyAsync(
            client,
            $"/api/v1/journal-entries/{voucherId}/submit?companyId={ErpApiFactory.DevCompanyId}"))
        {
            Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
        }

        Assert.Equal(2, await CountLedgerRowsAsync(voucherId)); // the voucher really reached GL

        using var reportResponse = await client.GetAsync(
            $"/api/v1/FinancialReports/trial-balance"
            + $"?companyId={ErpApiFactory.DevCompanyId}"
            + $"&asOfDate={EverythingCutoff:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, reportResponse.StatusCode);

        var report = (JsonObject)JsonNode.Parse(await reportResponse.Content.ReadAsStringAsync())!;
        AssertExactKeys(report, "asOfDate", "rows", "totalDebit", "totalCredit", "difference");

        // THE acceptance: "Trial balance reports zero discrepancy".
        Assert.Equal(0m, report["difference"]!.GetValue<decimal>());
        Assert.Equal(
            report["totalDebit"]!.GetValue<decimal>(),
            report["totalCredit"]!.GetValue<decimal>());

        // Not empty, not mis-scoped: the columns must match a whole-company SQL aggregate over the
        // exact same cutoff, which also proves the tenant filter did not narrow the statement.
        var oracle = await ReadTotalsAsync(EverythingCutoff);
        Assert.True(oracle.RowCount > 0, "The seeded ledger must not be empty.");
        Assert.Equal(oracle.TotalDebit, report["totalDebit"]!.GetValue<decimal>());
        Assert.Equal(oracle.TotalCredit, report["totalCredit"]!.GetValue<decimal>());

        // Row-level truth: one line per account that moved, and each line is that account's own
        // SUM(Debit)/SUM(Credit) straight from the ledger.
        var rows = report["rows"]!.AsArray();
        Assert.NotEmpty(rows);

        var oracleRows = await ReadAccountBalancesAsync(EverythingCutoff);
        Assert.Equal(oracleRows.Count, rows.Count);

        decimal summedDebit = 0m;
        decimal summedCredit = 0m;
        var codes = new List<string>();

        foreach (var node in rows)
        {
            var row = (JsonObject)node!;
            AssertExactKeys(
                row,
                "accountId",
                "accountCode",
                "accountName",
                "rootType",
                "totalDebit",
                "totalCredit",
                "netBalance");

            var code = row["accountCode"]!.GetValue<string>();
            codes.Add(code);

            var expected = oracleRows[code];
            Assert.Equal(expected.Debit, row["totalDebit"]!.GetValue<decimal>());
            Assert.Equal(expected.Credit, row["totalCredit"]!.GetValue<decimal>());

            // netBalance is the TECHNICAL Debit − Credit of the worksheet (plan.md §4), so credit
            // balances are legitimately negative here.
            Assert.Equal(
                expected.Debit - expected.Credit,
                row["netBalance"]!.GetValue<decimal>());

            summedDebit += row["totalDebit"]!.GetValue<decimal>();
            summedCredit += row["totalCredit"]!.GetValue<decimal>();
        }

        // The rows themselves must add up to the reported columns AND close at zero: two
        // independent ways to reach the same "zero discrepancy" the header claims.
        Assert.Equal(report["totalDebit"]!.GetValue<decimal>(), summedDebit);
        Assert.Equal(report["totalCredit"]!.GetValue<decimal>(), summedCredit);
        Assert.Equal(0m, summedDebit - summedCredit);

        // plan.md §4 ordering: AccountCode ascending (ordinal, exactly like the handler).
        var sorted = codes.OrderBy(c => c, StringComparer.Ordinal).ToArray();
        Assert.Equal(sorted, codes.ToArray());

        // The two accounts the acceptance voucher touched must be present with their new totals.
        Assert.Contains(codes, code => code == "1110");
        Assert.Contains(codes, code => code == "5110");
    }

    // ---------------------------------------------------------------------------------------------
    // GET balance-sheet + GET profit-and-loss - one story, told twice
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The balance sheet's three sections are the ledger's NATURAL-sign totals straight from SQL,
    /// Income/Expense never leak into them, the <c>balanced</c> flag is exactly the documented
    /// <c>|assets − (liabilities + equity)| ≤ 0.0001</c> test, and the residual of the accounting
    /// equation equals the P&amp;L's <c>netProfit</c> - the two statements cannot disagree because
    /// they are the same numbers read twice.
    /// </summary>
    [Fact]
    public async Task BalanceSheetAndProfitAndLoss_TellTheSameStory()
    {
        using var client = CreateClient();

        using var balanceSheetResponse = await client.GetAsync(
            $"/api/v1/FinancialReports/balance-sheet"
            + $"?companyId={ErpApiFactory.DevCompanyId}"
            + $"&asOfDate={EverythingCutoff:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, balanceSheetResponse.StatusCode);

        var balanceSheet = (JsonObject)JsonNode.Parse(
            await balanceSheetResponse.Content.ReadAsStringAsync())!;
        AssertExactKeys(balanceSheet, "assets", "liabilities", "equity", "balanced");

        using var profitAndLossResponse = await client.GetAsync(
            $"/api/v1/FinancialReports/profit-and-loss"
            + $"?companyId={ErpApiFactory.DevCompanyId}"
            + $"&from={EpochFrom}&to={EverythingCutoff:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, profitAndLossResponse.StatusCode);

        var profitAndLoss = (JsonObject)JsonNode.Parse(
            await profitAndLossResponse.Content.ReadAsStringAsync())!;
        AssertExactKeys(profitAndLoss, "revenue", "cogs", "expenses", "netProfit");

        // Every statement section is the pinned { rows, total } shape (FinancialSectionDto).
        AssertExactKeys(balanceSheet["assets"]!.AsObject(), "rows", "total");
        AssertExactKeys(balanceSheet["liabilities"]!.AsObject(), "rows", "total");
        AssertExactKeys(balanceSheet["equity"]!.AsObject(), "rows", "total");
        AssertExactKeys(profitAndLoss["revenue"]!.AsObject(), "rows", "total");
        AssertExactKeys(profitAndLoss["cogs"]!.AsObject(), "rows", "total");
        AssertExactKeys(profitAndLoss["expenses"]!.AsObject(), "rows", "total");

        // ---- The oracle: natural signs computed from the raw ledger, cutoff to cutoff. ----
        var oracle = await ReadSectionTotalsAsync(EverythingCutoff);

        Assert.Equal(oracle.Assets, SectionTotal(balanceSheet, "assets"));
        Assert.Equal(oracle.Liabilities, SectionTotal(balanceSheet, "liabilities"));
        Assert.Equal(oracle.Equity, SectionTotal(balanceSheet, "equity"));
        Assert.Equal(oracle.Revenue, SectionTotal(profitAndLoss, "revenue"));
        Assert.Equal(oracle.Cogs, SectionTotal(profitAndLoss, "cogs"));
        Assert.Equal(oracle.Expenses, SectionTotal(profitAndLoss, "expenses"));

        // Section totals are the sum of their own rows - a header can never drift from its body.
        AssertSectionRowsAddUp(balanceSheet, "assets");
        AssertSectionRowsAddUp(balanceSheet, "liabilities");
        AssertSectionRowsAddUp(balanceSheet, "equity");
        AssertSectionRowsAddUp(profitAndLoss, "revenue");
        AssertSectionRowsAddUp(profitAndLoss, "cogs");
        AssertSectionRowsAddUp(profitAndLoss, "expenses");

        // Income and Expense belong to the P&L: they must be ABSENT from the balance sheet.
        var excludedCodes = await ReadNonBalanceSheetCodesAsync();
        Assert.NotEmpty(excludedCodes);
        foreach (var section in new[] { "assets", "liabilities", "equity" })
        {
            foreach (var row in balanceSheet[section]!["rows"]!.AsArray())
            {
                var code = row!["accountCode"]!.GetValue<string>();
                Assert.DoesNotContain(
                    excludedCodes,
                    excluded => string.Equals(excluded, code, StringComparison.Ordinal));
            }
        }

        // The pinned formula: revenue − COGS − expenses = net profit (a loss is just negative).
        var revenue = SectionTotal(profitAndLoss, "revenue");
        var cogs = SectionTotal(profitAndLoss, "cogs");
        var expenses = SectionTotal(profitAndLoss, "expenses");
        Assert.Equal(
            revenue - cogs - expenses,
            profitAndLoss["netProfit"]!.GetValue<decimal>());

        // The `balanced` flag is the documented tolerance test, asserted BOTH ways so a report
        // that hardcoded `true` (or `false`) cannot slip through: the live dev ledger is an
        // UNCLOSED period (expense activity with no income and no equity), so it reports false.
        var assets = SectionTotal(balanceSheet, "assets");
        var liabilities = SectionTotal(balanceSheet, "liabilities");
        var equity = SectionTotal(balanceSheet, "equity");
        var residual = assets - (liabilities + equity);

        Assert.Equal(
            decimal.Abs(residual) <= 0.0001m,
            balanceSheet["balanced"]!.GetValue<bool>());
        Assert.False(
            balanceSheet["balanced"]!.GetValue<bool>(),
            "An unclosed period must report balanced == false (the P&L has not been closed into "
            + "equity yet); if this failed, the dev ledger became closed - check the seeds.");

        // THE cross-report identity: because every voucher is zero-sum (Constitution III.1),
        // assets − (liabilities + equity) IS the profit the P&L just reported. One story, twice.
        Assert.Equal(profitAndLoss["netProfit"]!.GetValue<decimal>(), residual);
    }

    // ---------------------------------------------------------------------------------------------
    // Validation - the three RFC 7807 400 codes the endpoints own (no framework problems)
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// <c>company_required</c>: a missing/empty companyId is rejected the SAME way by every one of
    /// the four endpoints, so a caller cannot accidentally report on "no company" and read an
    /// empty statement back as if it were real.
    /// </summary>
    [Fact]
    public async Task MissingCompanyId_IsRejectedByEveryEndpointWithCompanyRequired()
    {
        using var client = CreateClient();

        var empty = Guid.Empty.ToString();
        var endpoints = new[]
        {
            $"/api/v1/FinancialReports/general-ledger?companyId={empty}",
            $"/api/v1/FinancialReports/trial-balance?companyId={empty}&asOfDate=2026-12-31",
            $"/api/v1/FinancialReports/balance-sheet?companyId={empty}&asOfDate=2026-12-31",
            $"/api/v1/FinancialReports/profit-and-loss?companyId={empty}&from={EpochFrom}&to=2026-12-31",
        };

        foreach (var endpoint in endpoints)
        {
            using var response = await client.GetAsync(endpoint);
            await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Invalid Company", "company_required");
        }
    }

    /// <summary>
    /// <c>date_required</c>: a snapshot statement without its cutoff, and a P&amp;L without its
    /// period, are refused instead of silently defaulting to "today" - which would render a
    /// statement over the wrong window and look perfectly plausible to the reader.
    /// </summary>
    [Fact]
    public async Task MissingStatementPeriod_IsRejectedWithDateRequired()
    {
        using var client = CreateClient();
        var companyId = ErpApiFactory.DevCompanyId;

        using (var response = await client.GetAsync(
            $"/api/v1/FinancialReports/trial-balance?companyId={companyId}"))
        {
            await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Missing Date", "date_required");
        }

        using (var response = await client.GetAsync(
            $"/api/v1/FinancialReports/balance-sheet?companyId={companyId}"))
        {
            await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Missing Date", "date_required");
        }

        // Only ONE bound of the period is missing - still a rejection, never a half-open window.
        using (var response = await client.GetAsync(
            $"/api/v1/FinancialReports/profit-and-loss?companyId={companyId}&to=2026-12-31"))
        {
            await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Missing Period", "date_required");
        }
    }

    /// <summary>
    /// <c>invalid_date</c>: a date that is well-formed but does not exist (2026-02-30 has no such
    /// day) is rejected on every endpoint, including the general-ledger's optional range - a typo
    /// must never silently shift or widen a statement.
    /// </summary>
    [Fact]
    public async Task UnparsableDate_IsRejectedWithInvalidDate()
    {
        using var client = CreateClient();
        var companyId = ErpApiFactory.DevCompanyId;

        using (var response = await client.GetAsync(
            $"/api/v1/FinancialReports/trial-balance?companyId={companyId}&asOfDate=2026-02-30"))
        {
            await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Invalid Date", "invalid_date");
        }

        using (var response = await client.GetAsync(
            $"/api/v1/FinancialReports/balance-sheet?companyId={companyId}&asOfDate=2026-02-30"))
        {
            await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Invalid Date", "invalid_date");
        }

        using (var response = await client.GetAsync(
            $"/api/v1/FinancialReports/profit-and-loss?companyId={companyId}&from={EpochFrom}&to=nope"))
        {
            await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Invalid Period", "invalid_date");
        }

        // The general ledger's bounds are OPTIONAL (null = no filter) but a POPULATED one parses.
        using (var response = await client.GetAsync(
            $"/api/v1/FinancialReports/general-ledger?companyId={companyId}&from=nope"))
        {
            await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Invalid Date Range", "invalid_date");
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Helpers - HTTP
    // ---------------------------------------------------------------------------------------------

    /// <summary>Tenant-scoped client (X-Tenant-ID only - see ErpApiFactory remarks on auth).</summary>
    private HttpClient CreateClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-ID", ErpApiFactory.DevTenantId.ToString());
        return client;
    }

    /// <summary>
    /// Creates ONE Draft through <c>POST /api/v1/journal-entries</c> (duplicated from
    /// <see cref="JournalEntriesApiTests"/>: that class is read-only reference material for this
    /// task and must not be touched).
    /// </summary>
    private static async Task<JsonNode> CreateDraftAsync(
        HttpClient client,
        DateOnly postingDate,
        string userRemark,
        params (Guid AccountId, decimal Debit, decimal Credit)[] lines)
    {
        var payload = new
        {
            companyId = ErpApiFactory.DevCompanyId,
            postingDate = postingDate.ToString(
                "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            type = "Standard", // JsonStringEnumConverter is registered in Program.cs
            userRemark,
            lines = lines
                .Select(line => new
                {
                    accountId = line.AccountId,
                    debit = line.Debit,
                    credit = line.Credit,
                })
                .ToArray(),
        };

        using var response = await client.PostAsJsonAsync("/api/v1/journal-entries", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.NotEqual(Guid.Empty, body["id"]!.GetValue<Guid>());
        return body;
    }

    /// <summary>
    /// POSTs a syntactically valid but EMPTY JSON body - the transition endpoints allow it, so
    /// "no concurrency token" is a legal request. Each call carries a FRESH <c>Idempotency-Key</c>
    /// because submit is a guarded ledger-posting mutation (Constitution VI.4).
    /// </summary>
    private static Task<HttpResponseMessage> PostEmptyBodyAsync(HttpClient client, string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        return client.SendAsync(request);
    }

    /// <summary>
    /// Asserts the RFC 7807 contract of a rejection: status line, <c>status</c>, <c>title</c> and
    /// the stable machine code in the <c>code</c> extension.
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
    /// Asserts an object carries EXACTLY the given keys - the pinned contract is a closed set, so
    /// a rename AND a silently added field both fail here instead of drifting apart unnoticed.
    /// </summary>
    private static void AssertExactKeys(JsonObject value, params string[] expected)
    {
        var actual = value.Select(pair => pair.Key).ToArray();

        foreach (var key in expected)
        {
            Assert.Contains(key, actual);
        }

        Assert.Equal(expected.Length, actual.Length);
    }

    /// <summary>Reads a section's <c>total</c> out of a parsed statement.</summary>
    private static decimal SectionTotal(JsonObject statement, string section)
        => statement[section]!["total"]!.GetValue<decimal>();

    /// <summary>Asserts a section's header total equals the sum of its own rows.</summary>
    private static void AssertSectionRowsAddUp(JsonObject statement, string section)
    {
        var sum = statement[section]!["rows"]!
            .AsArray()
            .Sum(row => row!["balance"]!.GetValue<decimal>());

        Assert.Equal(SectionTotal(statement, section), sum);
    }

    // ---------------------------------------------------------------------------------------------
    // Helpers - the SQL oracle (raw ADO.NET: no query filter, no change tracker in the way)
    // ---------------------------------------------------------------------------------------------

    /// <summary>Whole-company Debit/Credit totals plus the row count, up to the given cutoff.</summary>
    private static async Task<LedgerTotals> ReadTotalsAsync(DateOnly cutoff)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            @"SELECT CAST(COALESCE(SUM([Debit]), 0) AS decimal(18,4)),
                     CAST(COALESCE(SUM([Credit]), 0) AS decimal(18,4)),
                     COUNT_BIG(*)
              FROM dbo.GLEntry
              WHERE [CompanyId] = @CompanyId AND [PostingDate] <= @Cutoff;",
            connection);

        command.Parameters.AddWithValue("@CompanyId", ErpApiFactory.DevCompanyId);
        AddDateParameter(command, "@Cutoff", cutoff);

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "The aggregate query must always return one row.");
        return new LedgerTotals(reader.GetDecimal(0), reader.GetDecimal(1), reader.GetInt64(2));
    }

    /// <summary>
    /// Per-account SUM(Debit)/SUM(Credit) up to the cutoff, keyed by AccountCode - plan.md §4's
    /// canonical grouping, written independently of the handler.
    /// </summary>
    private static async Task<Dictionary<string, (decimal Debit, decimal Credit)>> ReadAccountBalancesAsync(
        DateOnly cutoff)
    {
        var balances = new Dictionary<string, (decimal Debit, decimal Credit)>(StringComparer.Ordinal);

        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            @"SELECT a.[AccountCode],
                     CAST(SUM(g.[Debit]) AS decimal(18,4)),
                     CAST(SUM(g.[Credit]) AS decimal(18,4))
              FROM dbo.GLEntry g
              INNER JOIN dbo.Account a ON a.[Id] = g.[AccountId]
              WHERE g.[CompanyId] = @CompanyId AND g.[PostingDate] <= @Cutoff
              GROUP BY a.[AccountCode]
              HAVING SUM(g.[Debit]) <> 0 OR SUM(g.[Credit]) <> 0;",
            connection);

        command.Parameters.AddWithValue("@CompanyId", ErpApiFactory.DevCompanyId);
        AddDateParameter(command, "@Cutoff", cutoff);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            balances.Add(reader.GetString(0), (reader.GetDecimal(1), reader.GetDecimal(2)));
        }

        return balances;
    }

    /// <summary>
    /// Every section total of both statements in ONE pass over the ledger, applying the NATURAL
    /// sign per root (Asset/Expense = Debit − Credit, everything else = Credit − Debit) exactly as
    /// the accounting equation defines it - independent of how the handlers are written.
    /// </summary>
    private static async Task<SectionTotals> ReadSectionTotalsAsync(DateOnly cutoff)
    {
        var totals = new SectionTotals();

        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            @"SELECT a.[RootType], COALESCE(a.[Type], N'Other'),
                     CAST(SUM(CASE WHEN a.[RootType] IN (N'Asset', N'Expense')
                                   THEN g.[Debit] - g.[Credit]
                                   ELSE g.[Credit] - g.[Debit] END) AS decimal(18,4))
              FROM dbo.GLEntry g
              INNER JOIN dbo.Account a ON a.[Id] = g.[AccountId]
              WHERE g.[CompanyId] = @CompanyId AND g.[PostingDate] <= @Cutoff
              GROUP BY a.[RootType], a.[Type];",
            connection);

        command.Parameters.AddWithValue("@CompanyId", ErpApiFactory.DevCompanyId);
        AddDateParameter(command, "@Cutoff", cutoff);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var rootType = reader.GetString(0);
            var accountType = reader.GetString(1);
            var natural = reader.GetDecimal(2);

            switch (rootType)
            {
                case "Asset":
                    totals.Assets += natural;
                    break;
                case "Liability":
                    totals.Liabilities += natural;
                    break;
                case "Equity":
                    totals.Equity += natural;
                    break;
                case "Income":
                    totals.Revenue += natural;
                    break;
                case "Expense" when accountType == "COGS":
                    totals.Cogs += natural;
                    break;
                case "Expense":
                    totals.Expenses += natural;
                    break;
            }
        }

        return totals;
    }

    /// <summary>
    /// Codes of every Income/Expense account of the company - the set the balance sheet must never
    /// render.
    /// </summary>
    private static async Task<List<string>> ReadNonBalanceSheetCodesAsync()
    {
        var codes = new List<string>();

        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            @"SELECT [AccountCode] FROM dbo.Account
              WHERE [CompanyId] = @CompanyId AND [RootType] IN (N'Income', N'Expense');",
            connection);

        command.Parameters.AddWithValue("@CompanyId", ErpApiFactory.DevCompanyId);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            codes.Add(reader.GetString(0));
        }

        return codes;
    }

    /// <summary>
    /// The chronologically first ledger row (PostingDate ASC, Id ASC) - what the report's first
    /// page item must be.
    /// </summary>
    private static async Task<(long Id, DateOnly PostingDate)> ReadFirstLedgerRowAsync()
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            @"SELECT TOP (1) [Id], [PostingDate] FROM dbo.GLEntry
              WHERE [CompanyId] = @CompanyId
              ORDER BY [PostingDate] ASC, [Id] ASC;",
            connection);

        command.Parameters.AddWithValue("@CompanyId", ErpApiFactory.DevCompanyId);

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "The seeded ledger must not be empty.");
        return (reader.GetInt64(0), DateOnly.FromDateTime(reader.GetDateTime(1)));
    }

    /// <summary>Row count of ONE voucher - proves the acceptance voucher really posted.</summary>
    private static async Task<int> CountLedgerRowsAsync(Guid voucherId)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT COUNT_BIG(*) FROM dbo.GLEntry WHERE [VoucherId] = @VoucherId;",
            connection);
        command.Parameters.AddWithValue("@VoucherId", voucherId);

        return checked((int)(long)(await command.ExecuteScalarAsync())!);
    }

    /// <summary>
    /// Binds a <see cref="DateOnly"/> as a true SQL <c>date</c> so the comparison never depends on
    /// the session's date format.
    /// </summary>
    private static void AddDateParameter(SqlCommand command, string name, DateOnly value)
    {
        command.Parameters.Add(name, SqlDbType.Date).Value = value.ToDateTime(TimeOnly.MinValue);
    }

    // ---------------------------------------------------------------------------------------------
    // Oracle value types
    // ---------------------------------------------------------------------------------------------

    /// <summary>Whole-ledger Debit/Credit totals and the number of rows they were summed over.</summary>
    private sealed record LedgerTotals(decimal TotalDebit, decimal TotalCredit, long RowCount);

    /// <summary>Natural-sign totals of every section of both statements.</summary>
    private sealed class SectionTotals
    {
        public decimal Assets { get; set; }
        public decimal Liabilities { get; set; }
        public decimal Equity { get; set; }
        public decimal Revenue { get; set; }
        public decimal Cogs { get; set; }
        public decimal Expenses { get; set; }
    }
}

