using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Erp.Api.IntegrationTests;

/// <summary>
/// Task 6.7 / scenarios BN-01, BN-05, BN-02, BN-03, BN-04 and BN-07, end to end over HTTP
/// against the LIVE dev container: statement import stages rows with zero ledger writes,
/// FITID re-imports de-duplicate, the rules engine matches, reconcile and quick-voucher post
/// atomically, and concurrent matchers resolve to exactly one winner.
/// </summary>
/// <remarks>
/// <para><b>The oracle is the DATABASE, not only the response.</b> BN-01 is a statement about
/// <c>GLEntry</c> itself ("zero accounting entries are posted"), so every test snapshots the
/// global ledger count around the ACT and asserts the delta - never an absolute total, which
/// other tests legitimately append to (provisioning-neutral, the FiscalPeriodLock pattern;
/// journal headers/vouchers created here stay behind like every other posting test leaves).</para>
/// <para><b>Serialized through <see cref="LedgerMutatingCollection"/>:</b> the delta snapshots
/// and the shared dev company freeze/ledger make parallel execution flaky.</para>
/// <para><b>Bank accounts are per-test rows</b> (fresh GUIDs wired to the seeded 1110 leaf),
/// deleted in a <c>finally</c> block in FK order, so re-runs stay idempotent.</para>
/// <para><b>Ids used:</b> GL leaves <c>1110 Cash</c>
/// (<c>a0000000-0000-4000-8000-000000001110</c>) and <c>5110 Office Supplies Expense</c>
/// (<c>a0000000-0000-4000-8000-000000005110</c>) from scripts/seed-dev-coa.sql.</para>
/// </remarks>
[Collection(LedgerMutatingCollection.Name)]
public class BankStatementImportApiTests : IClassFixture<ErpApiFactory>
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    private static readonly Guid CashAccountId = Guid.Parse("a0000000-0000-4000-8000-000000001110");
    private static readonly Guid ExpenseAccountId = Guid.Parse("a0000000-0000-4000-8000-000000005110");

    private readonly ErpApiFactory _factory;

    public BankStatementImportApiTests(ErpApiFactory factory) => _factory = factory;

    // ------------------------------------------------------------- BN-01 staging isolation

    /// <summary>
    /// BN-01 live: importing 1,000 CSV lines stages exactly 1,000 Unreconciled rows while the
    /// ledger delta is zero (201 + the exact summary).
    /// </summary>
    [Fact]
    public async Task Import_1000CsvLines_Stages1000UnreconciledWithZeroGLEntries()
    {
        using var client = CreateClient();
        var bankAccountId = await CreateBankAccountAsync("BN01 Isolation");

        try
        {
            var content = BuildCsv(1000, i => (DateOnly.FromDateTime(DateTime.UtcNow), $"STMT LINE {i:0000}", 10m));
            var glBefore = await CountGLEntryAsync();

            using var response = await PostImportAsync(client, bankAccountId, "bn01.csv", "CSV", content);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var summary = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
            Assert.Equal(1000, summary["totalTransactions"]!.GetValue<int>());
            Assert.Equal(1000, summary["importedCount"]!.GetValue<int>());
            Assert.Equal(0, summary["duplicateCount"]!.GetValue<int>());

            Assert.Equal(glBefore, await CountGLEntryAsync());
            Assert.Equal(1000, await CountStagingAsync(bankAccountId));
            Assert.Equal(1000, await CountStagingByStatusAsync(bankAccountId, "Unreconciled"));
        }
        finally
        {
            await DeleteBankAccountAsync(bankAccountId);
        }
    }

    // ---------------------------------------------------------------- BN-05 FITID de-duplication

    /// <summary>
    /// BN-05 live: re-importing the same OFX content de-duplicates every FITID
    /// (duplicates == 5, imported == 0) and stages nothing new.
    /// </summary>
    [Fact]
    public async Task Reimport_SameOfxContent_SkipsEveryFitidAsDuplicate()
    {
        using var client = CreateClient();
        var bankAccountId = await CreateBankAccountAsync("BN05 Dedup");

        try
        {
            var content = BuildOfx(5, i => (new DateOnly(2026, 10, 2), -15m, $"BN05-FITID-{i:000}", "MONTHLY BANK FEE"));

            using var first = await PostImportAsync(client, bankAccountId, "bn05.ofx", "OFX", content);
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
            var firstSummary = JsonNode.Parse(await first.Content.ReadAsStringAsync())!;
            Assert.Equal(5, firstSummary["importedCount"]!.GetValue<int>());
            Assert.Equal(0, firstSummary["duplicateCount"]!.GetValue<int>());

            // A fresh key: same FILE, new submission (the filter only replays the same key).
            using var second = await PostImportAsync(client, bankAccountId, "bn05.ofx", "OFX", content);
            Assert.Equal(HttpStatusCode.Created, second.StatusCode);
            var secondSummary = JsonNode.Parse(await second.Content.ReadAsStringAsync())!;
            Assert.Equal(5, secondSummary["totalTransactions"]!.GetValue<int>());
            Assert.Equal(0, secondSummary["importedCount"]!.GetValue<int>());
            Assert.Equal(5, secondSummary["duplicateCount"]!.GetValue<int>());

            Assert.Equal(5, await CountStagingAsync(bankAccountId));
        }
        finally
        {
            await DeleteBankAccountAsync(bankAccountId);
        }
    }

    // ------------------------------------------------------- BN-02 rules engine over the import

    /// <summary>
    /// OFX happy path: FITIDs, deposit/withdrawal directions, then a seeded rule run marks the
    /// STRIPE line Matched with persisted suggestions (BN-02).
    /// </summary>
    [Fact]
    public async Task ImportOfx_ThenRunRules_MatchesStripeLineWithPersistedSuggestions()
    {
        using var client = CreateClient();
        var bankAccountId = await CreateBankAccountAsync("BN02 Rules");
        var ruleName = $"BN-TEST-Stripe-{Guid.NewGuid():N}";

        try
        {
            var content = BuildOfx(3, i => i switch
            {
                0 => (new DateOnly(2026, 10, 1), 5400m, "BN02-FITID-000", "STRIPE PAYOUT REF 98234"),
                1 => (new DateOnly(2026, 10, 2), -15m, "BN02-FITID-001", "MONTHLY BANK FEE"),
                _ => (new DateOnly(2026, 10, 3), 100m, "BN02-FITID-002", "MISC DEPOSIT"),
            });

            using var import = await PostImportAsync(client, bankAccountId, "bn02.ofx", "OFX", content);
            Assert.Equal(HttpStatusCode.Created, import.StatusCode);

            await CreateRuleAsync(client, ruleName, "Contains", "STRIPE", "Customer");

            using var run = await PostWithKeyAsync(
                client, HttpMethod.Post, "/api/v1/bank-transactions/run-rules",
                new { companyId = ErpApiFactory.DevCompanyId });
            Assert.Equal(HttpStatusCode.OK, run.StatusCode);
            var runSummary = JsonNode.Parse(await run.Content.ReadAsStringAsync())!;
            Assert.Equal(1, runSummary["matchedCount"]!.GetValue<int>());

            using var list = await client.GetAsync(
                $"/api/v1/bank-transactions?companyId={ErpApiFactory.DevCompanyId}&status=Matched");
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            var matched = JsonNode.Parse(await list.Content.ReadAsStringAsync())!["items"]!.AsArray();
            var stripe = Assert.Single(matched, n =>
                string.Equals(n!["description"]!.GetValue<string>(), "STRIPE PAYOUT REF 98234", StringComparison.Ordinal));
            Assert.Equal("Customer", stripe!["suggestedPartyType"]!.GetValue<string>());
            Assert.Equal("BN02-FITID-000", stripe!["transactionId"]!.GetValue<string>());

            // Directions survived the OFX sign mapping: deposit vs withdrawal.
            var fee = (await GetTransactionsAsync(client, "Unreconciled"))
                .Single(n => n!["transactionId"]!.GetValue<string>() == "BN02-FITID-001");
            Assert.Equal(0m, fee!["deposit"]!.GetValue<decimal>());
            Assert.Equal(15m, fee!["withdrawal"]!.GetValue<decimal>());
        }
        finally
        {
            await DeleteRuleAsync(ruleName);
            await DeleteBankAccountAsync(bankAccountId);
        }
    }

    // ------------------------------------------------- reconcile against a posted GL voucher

    /// <summary>
    /// Reconcile flow live: a $40 fee line reconciles against a journal voucher created through
    /// the journal API (Dr 5110 / Cr 1110), then un-reconciles back to Unreconciled (BN-03/BN-06).
    /// Per-account net of the voucher stays zero; the reconcile itself appends no GL rows.
    /// </summary>
    [Fact]
    public async Task Reconcile_WithdrawalAgainstPostedJournalVoucher_ReconcilesThenUnreconciles()
    {
        using var client = CreateClient();
        var bankAccountId = await CreateBankAccountAsync("BN03 Reconcile");

        try
        {
            var content = BuildOfx(1, _ => (new DateOnly(2026, 10, 2), -40m, $"BN03-FITID-{Guid.NewGuid():N}", "MONTHLY BANK FEE"));
            using var import = await PostImportAsync(client, bankAccountId, "bn03.ofx", "OFX", content);
            Assert.Equal(HttpStatusCode.Created, import.StatusCode);

            var line = (await GetTransactionsAsync(client, "Unreconciled"))
                .Single(n => n!["bankAccountId"]!.GetValue<string>() == bankAccountId.ToString());
            var lineId = line!["id"]!.GetValue<Guid>();
            var rowVersion = line!["rowVersion"]!.GetValue<string>();

            var voucherId = await CreateAndSubmitJournalAsync(client, 40m);
            var glBefore = await CountGLEntryAsync();

            using var reconcile = await PostWithKeyAsync(
                client, HttpMethod.Post, $"/api/v1/bank-transactions/{lineId}/reconcile",
                new
                {
                    companyId = ErpApiFactory.DevCompanyId,
                    lines = new[] { new { paymentEntryId = (Guid?)null, glVoucherId = (Guid?)voucherId, amount = 40m } },
                    rowVersion,
                });
            Assert.Equal(HttpStatusCode.OK, reconcile.StatusCode);

            // Zero GL writes on the reconcile path; exactly one link row.
            Assert.Equal(glBefore, await CountGLEntryAsync());
            Assert.Equal(1, await CountLinksAsync(lineId));

            var staged = await ReadStagingAsync(lineId);
            Assert.Equal("Reconciled", staged.Status);
            Assert.Equal(40m, staged.Allocated);
            Assert.Equal(new DateOnly(2026, 10, 2), staged.Clearance);

            // The voucher itself balances overall (total debits equal total credits).
            var (voucherDebit, voucherCredit) = await ReadVoucherTotalsAsync(voucherId);
            Assert.Equal(40m, voucherDebit);
            Assert.Equal(voucherDebit, voucherCredit);

            using var unreconcile = await PostWithKeyAsync(
                client, HttpMethod.Post, $"/api/v1/bank-transactions/{lineId}/unreconcile",
                new { companyId = ErpApiFactory.DevCompanyId });
            Assert.Equal(HttpStatusCode.OK, unreconcile.StatusCode);
            Assert.Equal("Unreconciled", (await ReadStagingAsync(lineId)).Status);
            Assert.Equal(0, await CountLinksAsync(lineId));
        }
        finally
        {
            await DeleteBankAccountAsync(bankAccountId);
        }
    }

    // ---------------------------------------------------------------- BN-04 quick voucher live

    /// <summary>
    /// BN-04 live: an unreconciled $15 fee line becomes a balanced SUBMITTED voucher
    /// (Dr 5110 / Cr 1110) with the line Reconciled, in one call.
    /// </summary>
    [Fact]
    public async Task QuickVoucher_FeeLine_PostsBalancedVoucherAndReconciles()
    {
        using var client = CreateClient();
        var bankAccountId = await CreateBankAccountAsync("BN04 Voucher");

        try
        {
            var content = BuildOfx(1, _ => (new DateOnly(2026, 10, 2), -15m, $"BN04-FITID-{Guid.NewGuid():N}", "MONTHLY BANK FEE"));
            using var import = await PostImportAsync(client, bankAccountId, "bn04.ofx", "OFX", content);
            Assert.Equal(HttpStatusCode.Created, import.StatusCode);

            var line = (await GetTransactionsAsync(client, "Unreconciled"))
                .Single(n => n!["bankAccountId"]!.GetValue<string>() == bankAccountId.ToString());
            var lineId = line!["id"]!.GetValue<Guid>();
            var glBefore = await CountGLEntryAsync();

            using var voucher = await PostWithKeyAsync(
                client, HttpMethod.Post, $"/api/v1/bank-transactions/{lineId}/quick-voucher",
                new
                {
                    companyId = ErpApiFactory.DevCompanyId,
                    expenseAccountCode = "5110",
                    memo = "BN-04 bank fee",
                    rowVersion = line!["rowVersion"]!.GetValue<string>(),
                });
            Assert.Equal(HttpStatusCode.Created, voucher.StatusCode);

            var entry = JsonNode.Parse(await voucher.Content.ReadAsStringAsync())!;
            Assert.Equal("Submitted", entry["status"]!.GetValue<string>());
            var entryId = entry["id"]!.GetValue<Guid>();

            // Exactly two balanced ledger rows: Dr 5110 / Cr 1110 $15.
            Assert.Equal(glBefore + 2, await CountGLEntryAsync());
            var rows = await ReadLedgerRowsAsync(entryId);
            Assert.Equal(2, rows.Count);
            Assert.Contains(rows, r => r.Account == ExpenseAccountId && r.Debit == 15m && r.Credit == 0m);
            Assert.Contains(rows, r => r.Account == CashAccountId && r.Debit == 0m && r.Credit == 15m);

            var staged = await ReadStagingAsync(lineId);
            Assert.Equal("Reconciled", staged.Status);
            Assert.Equal(15m, staged.Allocated);
            Assert.Equal(1, await CountLinksAsync(lineId));
        }
        finally
        {
            await DeleteBankAccountAsync(bankAccountId);
        }
    }

    // ------------------------------------------------------- BN-07 concurrent matchers live

    /// <summary>
    /// BN-07 live: two concurrent reconciles of one line with the same RowVersion resolve to
    /// exactly one 200; the loser is a 409 <c>concurrency_conflict</c> with a single link row.
    /// </summary>
    [Fact]
    public async Task ConcurrentReconciles_SameRowVersion_ExactlyOneWins()
    {
        using var client = CreateClient();
        var bankAccountId = await CreateBankAccountAsync("BN07 Race");

        try
        {
            var content = BuildOfx(1, _ => (new DateOnly(2026, 10, 2), -25m, $"BN07-FITID-{Guid.NewGuid():N}", "MONTHLY BANK FEE"));
            using var import = await PostImportAsync(client, bankAccountId, "bn07.ofx", "OFX", content);
            Assert.Equal(HttpStatusCode.Created, import.StatusCode);

            var line = (await GetTransactionsAsync(client, "Unreconciled"))
                .Single(n => n!["bankAccountId"]!.GetValue<string>() == bankAccountId.ToString());
            var lineId = line!["id"]!.GetValue<Guid>();
            var rowVersion = line!["rowVersion"]!.GetValue<string>();

            var voucherId = await CreateAndSubmitJournalAsync(client, 25m);
            var glBefore = await CountGLEntryAsync();

            var first = PostWithKeyAsync(
                client, HttpMethod.Post, $"/api/v1/bank-transactions/{lineId}/reconcile",
                new
                {
                    companyId = ErpApiFactory.DevCompanyId,
                    lines = new[] { new { paymentEntryId = (Guid?)null, glVoucherId = (Guid?)voucherId, amount = 25m } },
                    rowVersion,
                });
            var second = PostWithKeyAsync(
                client, HttpMethod.Post, $"/api/v1/bank-transactions/{lineId}/reconcile",
                new
                {
                    companyId = ErpApiFactory.DevCompanyId,
                    lines = new[] { new { paymentEntryId = (Guid?)null, glVoucherId = (Guid?)voucherId, amount = 25m } },
                    rowVersion,
                });

            using var firstResponse = await first;
            using var secondResponse = await second;

            var statuses = new[] { firstResponse.StatusCode, secondResponse.StatusCode };
            Assert.Contains(HttpStatusCode.OK, statuses);
            Assert.Contains(HttpStatusCode.Conflict, statuses);

            var loser = firstResponse.StatusCode == HttpStatusCode.Conflict ? firstResponse : secondResponse;
            var problem = JsonNode.Parse(await loser.Content.ReadAsStringAsync())!;
            Assert.Equal("concurrency_conflict", problem["code"]!.GetValue<string>());

            // Exactly one winner: one link row, the line Reconciled, zero GL delta on reconcile.
            Assert.Equal(1, await CountLinksAsync(lineId));
            Assert.Equal("Reconciled", (await ReadStagingAsync(lineId)).Status);
            Assert.Equal(glBefore, await CountGLEntryAsync());
        }
        finally
        {
            await DeleteBankAccountAsync(bankAccountId);
        }
    }

    // ---------------------------------------------------------------------------------- helpers

    /// <summary>Tenant-scoped client: only X-Tenant-ID (see ErpApiFactory remarks - auth is NOT disabled).</summary>
    private HttpClient CreateClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-ID", ErpApiFactory.DevTenantId.ToString());
        return client;
    }

    private static Task<HttpResponseMessage> PostImportAsync(
        HttpClient client, Guid bankAccountId, string fileName, string format, string content)
    {
        var payload = new
        {
            companyId = ErpApiFactory.DevCompanyId,
            bankAccountId,
            fileName,
            format,
            content,
        };
        return PostWithKeyAsync(client, HttpMethod.Post, "/api/v1/bank-statement-imports", payload);
    }

    /// <summary>POSTs one JSON body with a FRESH Idempotency-Key (Constitution VI.4).</summary>
    private static Task<HttpResponseMessage> PostWithKeyAsync(
        HttpClient client, HttpMethod method, string url, object payload)
    {
        var request = new HttpRequestMessage(method, url)
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add(IdempotencyKeyHeader, Guid.NewGuid().ToString("N"));
        return client.SendAsync(request);
    }

    private static async Task<JsonNode[]> GetTransactionsAsync(HttpClient client, string status)
    {
        using var response = await client.GetAsync(
            $"/api/v1/bank-transactions?companyId={ErpApiFactory.DevCompanyId}&status={status}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!["items"]!.AsArray()
            .Select(n => n!).ToArray();
    }

    private static async Task CreateRuleAsync(
        HttpClient client, string ruleName, string conditionType, string pattern, string targetPartyType)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/bank-transaction-rules", new
        {
            companyId = ErpApiFactory.DevCompanyId,
            ruleName,
            priority = 1,
            bankAccountId = (Guid?)null,
            conditionType,
            pattern,
            targetPartyType,
            targetPartyId = (Guid?)null,
            autoCreateVoucher = false,
            targetExpenseAccountId = (Guid?)null,
            isActive = true,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    /// <summary>
    /// Creates and submits a Dr 5110 / Cr 1110 journal voucher through the journal API and
    /// returns its id (the GL counterpart the reconcile tests allocate against).
    /// </summary>
    private static async Task<Guid> CreateAndSubmitJournalAsync(HttpClient client, decimal amount)
    {
        using var draft = await client.PostAsJsonAsync("/api/v1/journal-entries", new
        {
            companyId = ErpApiFactory.DevCompanyId,
            postingDate = "2026-10-02",
            type = "Standard",
            userRemark = "Banking test counterpart",
            lines = new[]
            {
                new { accountId = ExpenseAccountId, debit = amount, credit = 0m },
                new { accountId = CashAccountId, debit = 0m, credit = amount },
            },
        });
        Assert.Equal(HttpStatusCode.Created, draft.StatusCode);
        var id = JsonNode.Parse(await draft.Content.ReadAsStringAsync())!["id"]!.GetValue<Guid>();

        using var submit = await PostWithKeyAsync(
            client, HttpMethod.Post, $"/api/v1/journal-entries/{id}/submit?companyId={ErpApiFactory.DevCompanyId}",
            new { });
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        return id;
    }

    private static string BuildCsv(int count, Func<int, (DateOnly Date, string Description, decimal Amount)> row)
    {
        var lines = new System.Text.StringBuilder("date,description,amount\n");
        for (var i = 0; i < count; i++)
        {
            var (date, description, amount) = row(i);
            lines.Append(date.ToString("yyyy-MM-dd")).Append(',').Append(description).Append(',')
                .AppendLine(amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
        }

        return lines.ToString();
    }

    private static string BuildOfx(int count, Func<int, (DateOnly Date, decimal Amount, string Fitid, string Name)> row)
    {
        var ofx = new System.Text.StringBuilder();
        for (var i = 0; i < count; i++)
        {
            var (date, amount, fitid, name) = row(i);
            ofx.AppendLine("<STMTTRN>");
            ofx.AppendLine("<TRNTYPE>DEBIT");
            ofx.AppendLine($"<DTPOSTED>{date:yyyyMMdd}");
            ofx.AppendLine($"<TRNAMT>{amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}");
            ofx.AppendLine($"<FITID>{fitid}");
            ofx.AppendLine($"<NAME>{name}");
            ofx.AppendLine("</STMTTRN>");
        }

        return ofx.ToString();
    }

    private static async Task<Guid> CreateBankAccountAsync(string name)
    {
        var id = Guid.NewGuid();
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "INSERT INTO dbo.BankAccount (Id, TenantId, CompanyId, AccountName, BankName, AccountNumber, Currency, GLAccountId, LastReconciledBalance, IsActive) "
            + "VALUES (@Id, @TenantId, @CompanyId, @Name, N'BN Test Bank', @Number, N'USD', @GlAccountId, 0, 1);",
            connection);
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@TenantId", ErpApiFactory.DevTenantId);
        command.Parameters.AddWithValue("@CompanyId", ErpApiFactory.DevCompanyId);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@Number", $"BN-{id:N}");
        command.Parameters.AddWithValue("@GlAccountId", CashAccountId);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    /// <summary>Deletes a test bank account with its staging rows, imports and links (FK order).</summary>
    private static async Task DeleteBankAccountAsync(Guid bankAccountId)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        foreach (var sql in new[]
        {
            "DELETE l FROM dbo.BankReconciliation l JOIN dbo.BankTransaction t ON t.Id = l.BankTransactionId WHERE t.BankAccountId = @Id;",
            "DELETE FROM dbo.BankTransaction WHERE BankAccountId = @Id;",
            "DELETE FROM dbo.BankStatementImport WHERE BankAccountId = @Id;",
            "DELETE FROM dbo.BankTransactionRule WHERE BankAccountId = @Id;",
            "DELETE FROM dbo.BankAccount WHERE Id = @Id;",
        })
        {
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Id", bankAccountId);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task DeleteRuleAsync(string ruleName)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "DELETE FROM dbo.BankTransactionRule WHERE CompanyId = @CompanyId AND RuleName = @Name;",
            connection);
        command.Parameters.AddWithValue("@CompanyId", ErpApiFactory.DevCompanyId);
        command.Parameters.AddWithValue("@Name", ruleName);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Row count of the whole ledger - the BN-01 "nothing was written" oracle.</summary>
    private static async Task<int> CountGLEntryAsync()
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.GLEntry;", connection);
        return checked((int)(long)(await command.ExecuteScalarAsync())!);
    }

    private static async Task<int> CountStagingAsync(Guid bankAccountId)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT COUNT_BIG(*) FROM dbo.BankTransaction WHERE BankAccountId = @Id;", connection);
        command.Parameters.AddWithValue("@Id", bankAccountId);
        return checked((int)(long)(await command.ExecuteScalarAsync())!);
    }

    private static async Task<int> CountStagingByStatusAsync(Guid bankAccountId, string status)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT COUNT_BIG(*) FROM dbo.BankTransaction WHERE BankAccountId = @Id AND Status = @Status;",
            connection);
        command.Parameters.AddWithValue("@Id", bankAccountId);
        command.Parameters.AddWithValue("@Status", status);
        return checked((int)(long)(await command.ExecuteScalarAsync())!);
    }

    private static async Task<(string Status, decimal Allocated, DateOnly? Clearance)> ReadStagingAsync(Guid id)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT Status, AllocatedAmount, ClearanceDate FROM dbo.BankTransaction WHERE Id = @Id;",
            connection);
        command.Parameters.AddWithValue("@Id", id);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (
            reader.GetString(0),
            reader.GetDecimal(1),
            await reader.IsDBNullAsync(2) ? null : DateOnly.FromDateTime(reader.GetDateTime(2)));
    }

    private static async Task<int> CountLinksAsync(Guid bankTransactionId)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT COUNT_BIG(*) FROM dbo.BankReconciliation WHERE BankTransactionId = @Id;", connection);
        command.Parameters.AddWithValue("@Id", bankTransactionId);
        return checked((int)(long)(await command.ExecuteScalarAsync())!);
    }

    private sealed record LedgerRow(Guid Account, decimal Debit, decimal Credit);

    private static async Task<IReadOnlyList<LedgerRow>> ReadLedgerRowsAsync(Guid voucherId)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT AccountId, Debit, Credit FROM dbo.GLEntry WHERE VoucherId = @Id;", connection);
        command.Parameters.AddWithValue("@Id", voucherId);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<LedgerRow>();
        while (await reader.ReadAsync())
        {
            rows.Add(new LedgerRow(reader.GetGuid(0), reader.GetDecimal(1), reader.GetDecimal(2)));
        }

        return rows;
    }

    /// <summary>Total debits and credits of one voucher (the balance oracle).</summary>
    private static async Task<(decimal Debit, decimal Credit)> ReadVoucherTotalsAsync(Guid voucherId)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT SUM(Debit), SUM(Credit) FROM dbo.GLEntry WHERE VoucherId = @Id;",
            connection);
        command.Parameters.AddWithValue("@Id", voucherId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetDecimal(0), reader.GetDecimal(1));
    }
}
