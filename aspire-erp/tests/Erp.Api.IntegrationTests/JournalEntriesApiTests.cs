using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Erp.Api.IntegrationTests;

/// <summary>
/// Tasks 2.3/2.4 end-to-end evidence for the manual voucher workflow
/// create (Draft) -&gt; submit (ledger append) -&gt; cancel (compensating reversal) against the LIVE
/// dev container: spec scenarios AC-01, AC-02, AC-03 and AC-07, plus the two 409 conflicts the
/// pipeline must never swallow (illegal state move, frozen period) and the optional
/// optimistic-concurrency token of the transition bodies.
/// </summary>
/// <remarks>
/// <para><b>The oracle is the DATABASE, not only the response.</b> A 200/400 only proves the API
/// answered; AC-01 and AC-02 are statements about GLEntry ITSELF ("two balanced records are
/// appended" / "zero records are written to GLEntry"). Every test therefore queries
/// <c>dbo.GLEntry</c> by <c>VoucherId</c> - a freshly generated GUID no other voucher can claim -
/// so the assertions stay immune to ledger rows other tests append (and nobody can delete them:
/// Constitution Article III.2 makes the ledger append-only).</para>
///
/// <para><b>Serialized with <see cref="FiscalPeriodLockApiTests"/> through
/// <see cref="LedgerMutatingCollection"/>.</b> That class asserts a GLOBAL ledger row count and
/// both classes flip the SAME <c>Company.FrozenAccountsDate</c>; running them side by side would
/// make one of them flaky. Same collection = one at a time, while the rest of the assembly keeps
/// its default parallelism.</para>
///
/// <para><b>Both route templates declared by the controller are exercised:</b> create/submit/cancel
/// use the tasks.md 2.4 literal path <c>/api/v1/journal-entries</c>, while the read-back uses the
/// Constitution VI.1 path <c>/api/v1/JournalEntries/{id}</c>.</para>
///
/// <para><b>Accounts</b> are the deterministic GUIDs of scripts/seed-dev-coa.sql:
/// <c>1000 Assets</c> (IsGroup = true, spec AC-03) and the leaves <c>1110 Cash and Cash
/// Equivalents</c> / <c>5110 Office Supplies Expense</c> (the exact pair of spec AC-01).</para>
///
/// <para><b>Known side effect:</b> every successful create/submit/cancel LEAVES its rows behind
/// (a JournalEntry header plus append-only ledger rows) - intentional, exactly like the
/// stock/purchase integration tests.</para>
/// </remarks>
[Collection(LedgerMutatingCollection.Name)]
public class JournalEntriesApiTests : IClassFixture<ErpApiFactory>
{
    /// <summary>Account 1000 "Assets" - the group (non-posting) account of spec AC-03.</summary>
    private static readonly Guid GroupAssetsAccount =
        Guid.Parse("a0000000-0000-4000-8000-000000001000");

    /// <summary>Account 1110 "Cash and Cash Equivalents" - active leaf of the dev company.</summary>
    private static readonly Guid CashAccount =
        Guid.Parse("a0000000-0000-4000-8000-000000001110");

    /// <summary>Account 5110 "Office Supplies Expense" - active leaf of the dev company.</summary>
    private static readonly Guid OfficeSuppliesAccount =
        Guid.Parse("a0000000-0000-4000-8000-000000005110");

    private readonly ErpApiFactory _factory;

    public JournalEntriesApiTests(ErpApiFactory factory) => _factory = factory;

    /// <summary>
    /// Spec AC-01 / tasks.md 2.4 acceptance ("Returns 201 Created and updates ledger balances
    /// atomically"): the accountant's voucher - debit 5110 for $350.00, credit 1110 for $350.00 -
    /// is created as a Draft with a gapless JV voucher and NO ledger rows, then submitted for a
    /// 200 that appends exactly two balanced rows.
    /// </summary>
    [Fact]
    public async Task Submit_BalancedDraft_Returns201Then200AndAppendsTwoBalancedLedgerRows()
    {
        using var client = CreateClient();

        var draft = await CreateDraftAsync(
            client,
            new DateOnly(2026, 3, 15),
            "AC-01: office supplies paid in cash",
            (OfficeSuppliesAccount, 350m, 0m),
            (CashAccount, 0m, 350m));

        var id = draft["id"]!.GetValue<Guid>();
        var voucherNo = draft["voucherNo"]!.GetValue<string>();

        // A Draft carries its number already (Constitution III.4 / ERPNext naming on save) and
        // writes NOTHING to the ledger - that is what makes the AC-02/AC-03 drafts possible.
        Assert.Equal("Draft", draft["status"]!.GetValue<string>());
        Assert.StartsWith("JV-2026-", voucherNo, StringComparison.Ordinal); // year of the posting date
        Assert.Equal(0, await CountLedgerRowsAsync(id));

        // tasks.md 2.4's literal path for the transition.
        using var submitResponse = await PostEmptyBodyAsync(
            client,
            $"/api/v1/journal-entries/{id}/submit?companyId={ErpApiFactory.DevCompanyId}");

        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
        var submitted = JsonNode.Parse(await submitResponse.Content.ReadAsStringAsync())!;
        Assert.Equal("Submitted", submitted["status"]!.GetValue<string>());
        Assert.Equal(voucherNo, submitted["voucherNo"]!.GetValue<string>());

        // Constitution VI.1's path for the read-back: BOTH declared templates must resolve.
        using var readResponse = await client.GetAsync(
            $"/api/v1/JournalEntries/{id}?companyId={ErpApiFactory.DevCompanyId}");
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);

        var rows = await ReadLedgerRowsAsync(id);
        Assert.Equal(2, rows.Count); // AC-01: "two balanced records are appended to GLEntry"

        Assert.All(rows, row =>
        {
            Assert.Equal(voucherNo, row.VoucherNo);
            Assert.Equal("JournalEntry", row.VoucherType);
            Assert.False(row.IsCancelled); // a submitted voucher has no reversal rows
            Assert.Equal(new DateOnly(2026, 3, 15), row.PostingDate); // frozen accounting date
        });

        // AC-01: "the net difference is exactly $0.00".
        Assert.Equal(350m, rows.Sum(row => row.Debit));
        Assert.Equal(350m, rows.Sum(row => row.Credit));
        Assert.Equal(0m, rows.Sum(row => row.Debit) - rows.Sum(row => row.Credit));

        // One line per spec: debit 5110, credit 1110.
        Assert.Contains(rows, row =>
            row.AccountId == OfficeSuppliesAccount && row.Debit == 350m && row.Credit == 0m);
        Assert.Contains(rows, row =>
            row.AccountId == CashAccount && row.Debit == 0m && row.Credit == 350m);
    }

    /// <summary>
    /// Spec AC-02: a Draft whose debits total $1,000.00 and credits $995.00 is accepted as a draft
    /// (the draft is exactly what the scenario starts from) but its submission is rejected with
    /// <c>double_entry_imbalance</c> and ZERO rows reach the ledger - the header stays a Draft,
    /// because status change and rows are ONE transaction.
    /// </summary>
    [Fact]
    public async Task Submit_ImbalancedDraft_Returns400ImbalanceAndWritesZeroLedgerRows()
    {
        using var client = CreateClient();

        var draft = await CreateDraftAsync(
            client,
            new DateOnly(2026, 3, 15),
            "AC-02: deliberately imbalanced draft",
            (OfficeSuppliesAccount, 1000m, 0m),
            (CashAccount, 0m, 995m));

        var id = draft["id"]!.GetValue<Guid>();
        Assert.Equal("Draft", draft["status"]!.GetValue<string>()); // drafts may be imbalanced

        using var submitResponse = await PostEmptyBodyAsync(
            client,
            $"/api/v1/journal-entries/{id}/submit?companyId={ErpApiFactory.DevCompanyId}");

        await AssertProblemAsync(
            submitResponse,
            HttpStatusCode.BadRequest,
            "Journal Entry Rejected",
            "double_entry_imbalance");

        // AC-02's literal "zero records are written to GLEntry".
        Assert.Equal(0, await CountLedgerRowsAsync(id));

        // The rejected submission must not have moved the workflow either: one transaction, and
        // it rolled back (read back through the tasks.md 2.4 path).
        using var readResponse = await client.GetAsync(
            $"/api/v1/journal-entries/{id}?companyId={ErpApiFactory.DevCompanyId}");
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        var reloaded = JsonNode.Parse(await readResponse.Content.ReadAsStringAsync())!;
        Assert.Equal("Draft", reloaded["status"]!.GetValue<string>());
    }

    /// <summary>
    /// Spec AC-03: a balanced voucher whose line targets the group account <c>1000 - Assets</c> is
    /// rejected with the spec's own token (snake_case wire spelling
    /// <c>posting_to_group_account_prohibited</c>) and leaves no ledger row behind.
    /// </summary>
    [Fact]
    public async Task Submit_GroupAccountDraft_Returns400PostingToGroupAccountAndWritesZeroLedgerRows()
    {
        using var client = CreateClient();

        // Balanced on purpose: the GROUP account - not the balance - must be the reason to fail.
        var draft = await CreateDraftAsync(
            client,
            new DateOnly(2026, 3, 15),
            "AC-03: posting straight into the Assets folder",
            (GroupAssetsAccount, 100m, 0m),
            (CashAccount, 0m, 100m));

        var id = draft["id"]!.GetValue<Guid>();
        Assert.Equal("Draft", draft["status"]!.GetValue<string>()); // structure rules only at create

        using var submitResponse = await PostEmptyBodyAsync(
            client,
            $"/api/v1/journal-entries/{id}/submit?companyId={ErpApiFactory.DevCompanyId}");

        await AssertProblemAsync(
            submitResponse,
            HttpStatusCode.BadRequest,
            "Journal Entry Rejected",
            "posting_to_group_account_prohibited");

        Assert.Equal(0, await CountLedgerRowsAsync(id)); // rejected BEFORE any row was built
    }

    /// <summary>
    /// Spec AC-07 / Constitution III.3: cancelling a Submitted voucher appends compensating rows
    /// (Debit/Credit swapped) and moves the header to Cancelled, while every ORIGINAL row stays
    /// byte-for-byte identical - the append-only law that forbids flipping a marker in place.
    /// </summary>
    [Fact]
    public async Task Cancel_SubmittedVoucher_AppendsSwappedReversalAndKeepsOriginalsUntouched()
    {
        using var client = CreateClient();

        var draft = await CreateDraftAsync(
            client,
            new DateOnly(2026, 3, 15),
            "AC-07: voucher that will be cancelled",
            (OfficeSuppliesAccount, 250m, 0m),
            (CashAccount, 0m, 250m));

        var id = draft["id"]!.GetValue<Guid>();
        var voucherNo = draft["voucherNo"]!.GetValue<string>();

        using (var submitResponse = await PostEmptyBodyAsync(
            client,
            $"/api/v1/journal-entries/{id}/submit?companyId={ErpApiFactory.DevCompanyId}"))
        {
            Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
        }

        var before = await ReadLedgerRowsAsync(id);
        Assert.Equal(2, before.Count);
        Assert.All(before, row => Assert.False(row.IsCancelled));

        using var cancelResponse = await PostEmptyBodyAsync(
            client,
            $"/api/v1/journal-entries/{id}/cancel?companyId={ErpApiFactory.DevCompanyId}");

        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        var cancelled = JsonNode.Parse(await cancelResponse.Content.ReadAsStringAsync())!;
        Assert.Equal("Cancelled", cancelled["status"]!.GetValue<string>());
        Assert.Equal(voucherNo, cancelled["voucherNo"]!.GetValue<string>());

        var after = await ReadLedgerRowsAsync(id);
        Assert.Equal(4, after.Count); // 2 originals + 2 compensating reversals

        // "historical audit records are preserved without in-place mutation": record equality
        // compares EVERY column, so this fails if a single byte of an original row changed.
        Assert.Equal(before, after.Where(row => !row.IsCancelled).ToList());

        var reversals = after.Where(row => row.IsCancelled).ToList();
        Assert.Equal(2, reversals.Count); // the CANCELLATION marker travels on the reversal rows

        foreach (var original in before)
        {
            var mirror = Assert.Single(
                reversals,
                row => row.AccountId == original.AccountId && row.Id != original.Id);

            Assert.Equal(original.Credit, mirror.Debit); // swapped, Constitution III.3
            Assert.Equal(original.Debit, mirror.Credit);
            Assert.Equal(original.VoucherNo, mirror.VoucherNo); // same voucher, same identity
            Assert.Equal(original.PostingDate, mirror.PostingDate); // ORIGINAL accounting date
        }

        // The voucher's net balance returns to exactly 0.0000 once both sets are counted.
        Assert.Equal(0m, after.Sum(row => row.Debit) - after.Sum(row => row.Credit));
        Assert.All(after, row => Assert.Equal(voucherNo, row.VoucherNo));
    }

    /// <summary>
    /// Workflow guard: cancelling a voucher that is still a Draft is an illegal state move, so the
    /// API answers 409 <c>invalid_status_transition</c> (the same wire value the buying workflow
    /// uses) and appends no reversal rows.
    /// </summary>
    [Fact]
    public async Task Cancel_DraftVoucher_Returns409InvalidStatusTransitionAndWritesNoRows()
    {
        using var client = CreateClient();

        var draft = await CreateDraftAsync(
            client,
            new DateOnly(2026, 3, 15),
            "Cancel a draft: illegal move",
            (OfficeSuppliesAccount, 40m, 0m),
            (CashAccount, 0m, 40m));

        var id = draft["id"]!.GetValue<Guid>();

        using var cancelResponse = await PostEmptyBodyAsync(
            client,
            $"/api/v1/journal-entries/{id}/cancel?companyId={ErpApiFactory.DevCompanyId}");

        await AssertProblemAsync(
            cancelResponse,
            HttpStatusCode.Conflict,
            "Journal Entry Conflict",
            "invalid_status_transition");

        Assert.Equal(0, await CountLedgerRowsAsync(id));

        // Still a Draft - the rejection changed nothing at all.
        using var readResponse = await client.GetAsync(
            $"/api/v1/journal-entries/{id}?companyId={ErpApiFactory.DevCompanyId}");
        var reloaded = JsonNode.Parse(await readResponse.Content.ReadAsStringAsync())!;
        Assert.Equal("Draft", reloaded["status"]!.GetValue<string>());
    }

    /// <summary>
    /// Spec AC-04 for this pipeline: with the company frozen at 2025-12-31, submitting a voucher
    /// dated 2025-12-15 fails with 409 <c>fiscal_period_locked</c> and leaves zero ledger rows.
    /// The freeze is ALWAYS restored in a <c>finally</c> block - a crashed run must never leave
    /// the shared dev database closed for the whole team.
    /// </summary>
    [Fact]
    public async Task Submit_WhenPeriodIsFrozen_Returns409FiscalPeriodLockedAndWritesZeroLedgerRows()
    {
        using var client = CreateClient();
        var originalFreeze = await ReadFrozenAccountsDateAsync();

        try
        {
            // Created BEFORE the freeze: the draft itself only validates structure, and this
            // keeps the test honest about which gate rejects the submission (the submit one).
            var draft = await CreateDraftAsync(
                client,
                new DateOnly(2025, 12, 15),
                "AC-04: back-dated voucher into a closed period",
                (OfficeSuppliesAccount, 75m, 0m),
                (CashAccount, 0m, 75m));

            var id = draft["id"]!.GetValue<Guid>();

            await WriteFrozenAccountsDateAsync(new DateOnly(2025, 12, 31));

            using var submitResponse = await PostEmptyBodyAsync(
                client,
                $"/api/v1/journal-entries/{id}/submit?companyId={ErpApiFactory.DevCompanyId}");

            var problem = await AssertProblemAsync(
                submitResponse,
                HttpStatusCode.Conflict,
                "Fiscal Period Locked",
                "fiscal_period_locked");

            // The detail names both dates so an operator can see what closed the period.
            var detail = problem["detail"]!.GetValue<string>();
            Assert.Contains("2025-12-15", detail, StringComparison.Ordinal);
            Assert.Contains("2025-12-31", detail, StringComparison.Ordinal);

            // AC-04 "no data is modified": the lock is the FIRST data gate.
            Assert.Equal(0, await CountLedgerRowsAsync(id));
        }
        finally
        {
            await WriteFrozenAccountsDateAsync(originalFreeze);
        }

        // Proof the finally block really handed the open books back to the next test/run.
        Assert.Equal(originalFreeze, await ReadFrozenAccountsDateAsync());
    }

    /// <summary>
    /// Optimistic concurrency contract of the transition bodies: a STALE <c>rowVersion</c> is a
    /// 409 <c>concurrency_conflict</c> with zero ledger rows, and the voucher stays perfectly
    /// submittable afterwards - the body is OPTIONAL, so retrying without a token still works
    /// (the store-generated token keeps guarding the load/save race on the server).
    /// </summary>
    [Fact]
    public async Task Submit_WithStaleRowVersion_Returns409ConflictThenSucceedsWithoutToken()
    {
        using var client = CreateClient();

        var draft = await CreateDraftAsync(
            client,
            new DateOnly(2026, 3, 15),
            "RowVersion compare-and-swap",
            (OfficeSuppliesAccount, 10m, 0m),
            (CashAccount, 0m, 10m));

        var id = draft["id"]!.GetValue<Guid>();

        // An all-zero rowversion can never equal the store-generated token of a real row.
        var staleToken = Convert.ToBase64String(new byte[8]);
        Assert.NotEqual(draft["rowVersion"]!.GetValue<string>(), staleToken);

        var conflictRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/journal-entries/{id}/submit?companyId={ErpApiFactory.DevCompanyId}")
        {
            Content = JsonContent.Create(new { rowVersion = staleToken }),
        };
        conflictRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));

        using var conflictResponse = await client.SendAsync(conflictRequest);

        await AssertProblemAsync(
            conflictResponse,
            HttpStatusCode.Conflict,
            "Concurrent Update Conflict",
            "concurrency_conflict");

        Assert.Equal(0, await CountLedgerRowsAsync(id));

        // Empty body = no client token = the second (legal) shape of the same endpoint.
        using var successResponse = await PostEmptyBodyAsync(
            client,
            $"/api/v1/journal-entries/{id}/submit?companyId={ErpApiFactory.DevCompanyId}");

        Assert.Equal(HttpStatusCode.OK, successResponse.StatusCode);
        Assert.Equal(2, await CountLedgerRowsAsync(id));
    }

    /// <summary>Tenant-scoped client (X-Tenant-ID only - see ErpApiFactory remarks on auth).</summary>
    private HttpClient CreateClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-ID", ErpApiFactory.DevTenantId.ToString());
        return client;
    }

    /// <summary>
    /// Creates ONE Draft through <c>POST /api/v1/journal-entries</c> (tasks.md 2.4's literal path)
    /// and asserts the 201 Created contract of tasks.md 2.4. Amounts and accounts are passed in so
    /// each scenario reads like the spec bullet it implements.
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
            postingDate = postingDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
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
        Assert.NotNull(response.Headers.Location); // 201 Created carries where it landed

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.NotEqual(Guid.Empty, body["id"]!.GetValue<Guid>());
        Assert.Equal(2, body["lines"]!.AsArray().Count);
        return body;
    }

    /// <summary>
    /// POSTs a syntactically valid but EMPTY JSON body: the transition endpoints declare
    /// <c>EmptyBodyBehavior.Allow</c>, so "no concurrency token" must be a legal request.
    /// Each call carries a FRESH <c>Idempotency-Key</c> because submit/cancel are guarded
    /// mutations (Constitution VI.4) and the filter rejects requests without the header.
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
    /// Asserts the RFC 7807 contract of a rejection: status line, <c>status</c> extension and the
    /// stable machine code in the <c>code</c> extension (the mapping lives in
    /// JournalEntriesController - workflow/lock/concurrency conflicts are 409, 404 is "not found",
    /// everything else is 400). Returns the parsed problem for extra detail assertions.
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
    /// Reads EVERY ledger row of one voucher from the live container - the AC-01/AC-02/AC-07
    /// oracle ("appended", "zero records", "preserved without in-place mutation").
    /// </summary>
    private static async Task<List<LedgerRow>> ReadLedgerRowsAsync(Guid voucherId)
    {
        var rows = new List<LedgerRow>();

        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            @"SELECT [Id], [AccountId], [Debit], [Credit], [IsCancelled], [PostingDate],
                     [VoucherType], [VoucherNo], [Remarks]
              FROM dbo.GLEntry
              WHERE [VoucherId] = @VoucherId
              ORDER BY [Id];",
            connection);
        command.Parameters.AddWithValue("@VoucherId", voucherId);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new LedgerRow(
                reader.GetInt64(0),
                reader.GetGuid(1),
                reader.GetDecimal(2),
                reader.GetDecimal(3),
                reader.GetBoolean(4),
                DateOnly.FromDateTime(reader.GetDateTime(5)),
                reader.GetString(6),
                reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8)));
        }

        return rows;
    }

    /// <summary>Row count of ONE voucher - the "zero records are written" assertion.</summary>
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

    /// <summary>Reads the live dev company's freeze boundary (NULL = books are open).</summary>
    private static async Task<DateOnly?> ReadFrozenAccountsDateAsync()
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT [FrozenAccountsDate] FROM dbo.Company WHERE Id = @Id;", connection);
        command.Parameters.AddWithValue("@Id", ErpApiFactory.DevCompanyId);

        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? null : DateOnly.FromDateTime((DateTime)value);
    }

    /// <summary>
    /// Writes the freeze boundary directly (raw SQL, not EF) so no tracked entity of this process
    /// can interfere with what the API observes.
    /// </summary>
    private static async Task WriteFrozenAccountsDateAsync(DateOnly? value)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "UPDATE dbo.Company SET [FrozenAccountsDate] = @Value WHERE Id = @Id;", connection);
        command.Parameters.AddWithValue("@Id", ErpApiFactory.DevCompanyId);
        command.Parameters.Add("@Value", SqlDbType.Date).Value =
            value is null ? (object)DBNull.Value : value.Value.ToDateTime(TimeOnly.MinValue);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Projection of one dbo.GLEntry row - value-equal, so it can prove "byte-identical".</summary>
    private sealed record LedgerRow(
        long Id,
        Guid AccountId,
        decimal Debit,
        decimal Credit,
        bool IsCancelled,
        DateOnly PostingDate,
        string VoucherType,
        string VoucherNo,
        string? Remarks);
}

