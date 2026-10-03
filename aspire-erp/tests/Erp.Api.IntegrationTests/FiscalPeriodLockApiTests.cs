using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Erp.Api.IntegrationTests;

/// <summary>
/// Task 2.2 / spec AC-04 acceptance, end to end: once <c>Company.FrozenAccountsDate</c> is set,
/// a POST to a ledger-posting endpoint dated ON or BEFORE that day must come back as
/// RFC 7807 409 Conflict with the stable <c>fiscal_period_locked</c> code, and must leave the
/// General Ledger byte-for-byte untouched ("no data is modified").
/// </summary>
/// <remarks>
/// <para><b>Why the freeze is set and restored by this test:</b> the dev company ships with a
/// NULL freeze (a running business is open), so exercising the lock means temporarily freezing
/// it. The value is read first and ALWAYS written back in a <c>finally</c> block - a test that
/// crashed halfway must never leave the shared dev database frozen, or every later posting would
/// fail for the whole team.</para>
///
/// <para><b>Side effect to know about:</b> Company is a system-versioned (temporal) table, so the
/// freeze + restore writes two CompanyHistory rows per run. That is inherent to mutating a
/// temporal entity and is harmless: the live row ends exactly where it started.</para>
///
/// <para><b>Why a bogus warehouseId is intentional:</b> StockPostingService checks the fiscal
/// lock IMMEDIATELY after loading the company, BEFORE warehouse and item resolution - so the
/// request can never reach an unrelated "warehouse not found" 400, and a 409 here proves the lock
/// is the first gate, before any GLEntry line exists (AC-04).</para>
///
/// <para><b>Serialized with <see cref="JournalEntriesApiTests"/></b> through
/// <see cref="LedgerMutatingCollection"/>: this test asserts a GLOBAL ledger row count and flips
/// the SAME freeze date the journal pipeline posts against, so the two classes must not overlap.</para>
/// </remarks>
[Collection(LedgerMutatingCollection.Name)]
public class FiscalPeriodLockApiTests : IClassFixture<ErpApiFactory>
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    private readonly ErpApiFactory _factory;

    public FiscalPeriodLockApiTests(ErpApiFactory factory) => _factory = factory;

    [Fact]
    public async Task PostStockEntry_WhenCompanyIsFrozen_RejectsOnAndBeforeFreezeDateWith409AndNoNewLedgerRows()
    {
        var originalFreeze = await ReadFrozenAccountsDateAsync();
        var glRowsBefore = await CountGLEntryAsync();

        try
        {
            // Freeze at the boundary: AC-04 rejects "on or before", so BOTH a strictly earlier
            // posting date and the freeze date itself must be refused by the SAME rule.
            var freezeDate = new DateOnly(2025, 12, 31);
            await WriteFrozenAccountsDateAsync(freezeDate);

            await AssertLockedAsync("2025-12-15", "2025-12-31"); // strictly before the boundary
            await AssertLockedAsync("2025-12-31", "2025-12-31"); // exactly on the boundary

            // AC-04 in one number: not a single ledger line was appended or amended.
            Assert.Equal(glRowsBefore, await CountGLEntryAsync());
        }
        finally
        {
            await WriteFrozenAccountsDateAsync(originalFreeze);
        }

        // Proof the finally block really restored the books for the next run / the next test.
        Assert.Equal(originalFreeze, await ReadFrozenAccountsDateAsync());
    }

    /// <summary>
    /// Sends one back-dated material receipt and asserts the RFC 7807 contract of the rejection.
    /// </summary>
    private async Task AssertLockedAsync(string postingDate, string expectedBoundary)
    {
        using var client = CreateClient();

        // A fresh key per attempt: the idempotency filter releases the reservation again on a
        // 4xx, but a new key keeps the two attempts independent of that bookkeeping.
        client.DefaultRequestHeaders.Add(IdempotencyKeyHeader, Guid.NewGuid().ToString("N"));

        var payload = new
        {
            companyId = ErpApiFactory.DevCompanyId,
            entryType = "MaterialReceipt", // JsonStringEnumConverter is registered in Program.cs
            warehouseId = Guid.NewGuid(),   // never resolved - the lock fires first (see remarks)
            postingDate,
            lines = new[] { new { itemId = Guid.NewGuid(), qty = 1m, rate = 10m } },
        };

        using var response = await client.PostAsJsonAsync("/api/v1/stockentries", payload);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal(409, problem["status"]!.GetValue<int>());
        Assert.Equal("fiscal_period_locked", problem["code"]!.GetValue<string>());
        Assert.Equal("Fiscal Period Locked", problem["title"]!.GetValue<string>());

        // The detail carries BOTH dates so an operator can see what closed the period.
        var detail = problem["detail"]!.GetValue<string>();
        Assert.Contains(postingDate, detail);
        Assert.Contains(expectedBoundary, detail);
    }

    /// <summary>Tenant-scoped client (X-Tenant-ID only - see ErpApiFactory remarks on auth).</summary>
    private HttpClient CreateClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-ID", ErpApiFactory.DevTenantId.ToString());
        return client;
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
    /// Writes the freeze boundary directly (raw SQL, not EF) so no tracked entity or change
    /// tracker of the test process can interfere with what the API observes.
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

    /// <summary>Row count of the whole ledger - the AC-04 "nothing was written" oracle.</summary>
    private static async Task<int> CountGLEntryAsync()
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.GLEntry;", connection);
        return checked((int)(long)(await command.ExecuteScalarAsync())!);
    }
}

