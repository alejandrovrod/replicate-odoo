using Erp.Application.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Api.IntegrationTests;

/// <summary>
/// Task 2.1 evidence for Constitution Article III.2 ("the General Ledger is append-only") against
/// the LIVE dev SQL container: the two enforcement lines of defence are proved on REAL posted rows
/// rather than on a mock - first the application-level guard inside
/// <c>AppDbContext.SaveChanges</c>, then the <c>trg_GLEntry_AppendOnly</c> INSTEAD OF trigger that
/// covers raw SQL (sqlcmd, SSMS, ETL) which the ORM cannot see.
/// </summary>
/// <remarks>
/// <para><b>Why no HTTP and why no fixture:</b> nothing here goes through Erp.Api - these are
/// persistence-level guarantees, so the class talks to the container directly and only borrows
/// <see cref="ErpApiFactory"/>'s connection/tenant constants (a <c>WebApplicationFactory</c>
/// would add a whole host for no extra coverage).</para>
///
/// <para><b>Why every tampering attempt must be rolled back or rejected:</b> the trigger rejects
/// UPDATE and DELETE outright, so committed ledger rows can NEVER be cleaned up by hand. Each
/// test therefore either lets the guard throw before any SQL is sent (app level) or lets the
/// trigger reject the statement (database level) and then re-reads the row to prove it is
/// byte-for-byte what the posting engine wrote. Nothing in this class leaves a row behind.</para>
///
/// <para><b>Tenant:</b> <c>AppDbContext</c> attaches a global query filter driven by
/// <see cref="ITenantProvider"/>, so a stub pinned to the dev tenant is what makes these reads
/// (and the injected insert below) resolve against the seeded COA - fail-closed, not bypassed.</para>
/// </remarks>
public class GLEntryLedgerGuardTests
{
    /// <summary>
    /// Article III.2, FIRST line of defence: a tracked GLEntry whose value was changed aborts the
    /// save with <see cref="GLEntryAppendOnlyViolationException"/> BEFORE a single UPDATE statement
    /// is composed - the ledger row keeps the exact bytes the posting engine wrote.
    /// </summary>
    [Fact]
    public async Task SaveChanges_WhenGLEntryIsModified_ThrowsAppendOnlyViolationAndWritesNothing()
    {
        // Seed the target from a committed row (any posted line works - the rule is table-wide).
        long id;
        string originalRemarks;

        await using (var reader = CreateContext())
        {
            var posted = await reader.GLEntries.OrderBy(e => e.Id).FirstAsync();
            id = posted.Id;
            originalRemarks = posted.Remarks ?? string.Empty;
        }

        await using (var tamperer = CreateContext())
        {
            var entry = await tamperer.GLEntries.FirstAsync(e => e.Id == id);
            entry.Remarks = originalRemarks + " | tampered-by-test";

            var ex = await Assert.ThrowsAsync<GLEntryAppendOnlyViolationException>(
                () => tamperer.SaveChangesAsync());

            Assert.Contains("UPDATE", ex.Message);
            Assert.Equal(StockErrorCodes.GlEntryAppendOnly, ex.Code);
        }

        // Independent context = independent change tracker, so this reads what the DATABASE holds.
        await using var verifier = CreateContext();
        var reloaded = await verifier.GLEntries.FirstAsync(e => e.Id == id);
        Assert.Equal(originalRemarks, reloaded.Remarks ?? string.Empty);
    }

    /// <summary>
    /// Article III.2: DELETING a posted line is equally illegal - a ledger row is never removed,
    /// a compensating reversal is posted instead (Article III.3). The tracked delete must abort
    /// the save and leave the row queryable.
    /// </summary>
    [Fact]
    public async Task SaveChanges_WhenGLEntryIsRemoved_ThrowsAppendOnlyViolationAndKeepsRow()
    {
        long id;

        await using (var remover = CreateContext())
        {
            var entry = await remover.GLEntries.OrderBy(e => e.Id).FirstAsync();
            id = entry.Id;

            remover.GLEntries.Remove(entry);

            var ex = await Assert.ThrowsAsync<GLEntryAppendOnlyViolationException>(
                () => remover.SaveChangesAsync());

            Assert.Contains("DELETE", ex.Message);
            Assert.Equal(StockErrorCodes.GlEntryAppendOnly, ex.Code);
        }

        await using var verifier = CreateContext();
        Assert.True(
            await verifier.GLEntries.AnyAsync(e => e.Id == id),
            $"GLEntry {id} must survive the rejected DELETE (Constitution Article III.2).");
    }

    /// <summary>
    /// Article III.2, SECOND line of defence: a raw T-SQL UPDATE bypasses the ORM entirely, so it
    /// can only be stopped by <c>trg_GLEntry_AppendOnly</c>. The trigger THROWs 51000, which
    /// surfaces as <see cref="SqlException"/> with that exact number and the ledger's message -
    /// hard evidence that the database itself refuses the mutation, independent of any C# code.
    /// </summary>
    [Fact]
    public async Task RawSqlUpdate_WhenTargetingPostedRow_IsRejectedByDatabaseTrigger()
    {
        long id;
        string originalRemarks;

        await using (var reader = CreateContext())
        {
            var posted = await reader.GLEntries.OrderBy(e => e.Id).FirstAsync();
            id = posted.Id;
            originalRemarks = posted.Remarks ?? string.Empty;
        }

        using (var connection = new SqlConnection(ErpApiFactory.DevConnectionString))
        {
            await connection.OpenAsync();

            // Deliberately ADO.NET, not EF: this must prove the trigger, not the change tracker.
            await using var command = new SqlCommand(
                "UPDATE dbo.GLEntry SET Remarks = N'tampered-by-raw-sql' WHERE Id = @Id;",
                connection);
            command.Parameters.AddWithValue("@Id", id);

            var ex = await Assert.ThrowsAsync<SqlException>(
                () => command.ExecuteNonQueryAsync());

            Assert.Equal(51000, ex.Number);
            Assert.Contains("GLEntry is append-only", ex.Message);
        }

        await using var verifier = CreateContext();
        var reloaded = await verifier.GLEntries.FirstAsync(e => e.Id == id);
        Assert.Equal(originalRemarks, reloaded.Remarks ?? string.Empty);
    }

    /// <summary>
    /// Task 2.1 schema evidence: plan §2 renamed the CHECK constraints to
    /// <c>CK_Debit_NonNegative</c> / <c>CK_Credit_NonNegative</c>. Inserting a negative Debit
    /// through EF (so the append-only trigger sees a legal INSERT and passes it through) must be
    /// rejected by the CONSTRAINT itself - SQL Server error 547 naming the constraint - which
    /// proves the predicate survived the rename and still guards the column.
    /// </summary>
    [Fact]
    public async Task Insert_WithNegativeDebit_IsRejectedByNonNegativeCheckConstraint()
    {
        await using var context = CreateContext();

        // A real, active account of the dev company: FK_GLEntry_Account would otherwise mask the
        // CHECK failure with a foreign-key error, and we would be proving the wrong thing.
        var accountId = await context.Accounts
            .Where(a => a.CompanyId == ErpApiFactory.DevCompanyId)
            .OrderBy(a => a.AccountCode)
            .Select(a => a.Id)
            .FirstAsync();

        var voucherNo = $"GLGUARD-{Guid.NewGuid().ToString("N")[..8]}";

        context.GLEntries.Add(new GLEntry
        {
            TenantId = ErpApiFactory.DevTenantId,
            CompanyId = ErpApiFactory.DevCompanyId,
            PostingDate = new DateOnly(2026, 1, 1),
            AccountId = accountId,
            Debit = -1m, // violates [Debit] >= 0 on purpose
            Credit = 0m,
            VoucherType = "StockEntry",
            VoucherNo = voucherNo,
            VoucherId = Guid.NewGuid(),
        });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        Assert.Contains("CK_Debit_NonNegative", ex.ToString());

        var sqlException = Assert.IsType<SqlException>(ex.InnerException);
        Assert.Equal(547, sqlException.Number); // CHECK constraint conflict

        // A rejected INSERT leaves nothing behind - no sentinel row, no identity burn worth
        // guarding, but above all no half-written ledger line.
        await using var verifier = CreateContext();
        Assert.False(
            await verifier.GLEntries.AnyAsync(e => e.VoucherNo == voucherNo),
            "The negative-Debit line must not have reached the ledger.");
    }

    /// <summary>
    /// Builds a context bound to the live dev container and pinned to the dev tenant (see class
    /// remarks - the global query filter is driven by the stub, never bypassed).
    /// </summary>
    private static AppDbContext CreateContext() =>
        new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(ErpApiFactory.DevConnectionString)
                .Options,
            new StubTenantProvider(ErpApiFactory.DevTenantId));

    /// <summary>
    /// Minimal <see cref="ITenantProvider"/> for direct context construction: reads resolve to
    /// the dev tenant, writes are pinned to it by <c>EnforceTenantInvariants</c>, and there is no
    /// request scope to <see cref="ITenantProvider.SetCurrentTenantId"/> from.
    /// </summary>
    private sealed class StubTenantProvider : ITenantProvider
    {
        private Guid _tenantId;

        public StubTenantProvider(Guid tenantId) => _tenantId = tenantId;

        public Guid GetCurrentTenantId() => _tenantId;

        public bool HasTenant() => _tenantId != Guid.Empty;

        public void SetCurrentTenantId(Guid tenantId) => _tenantId = tenantId;
    }
}

