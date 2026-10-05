using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Erp.Api.IntegrationTests;

/// <summary>
/// Task 12.5 / specs HR-01..HR-06 live: the full October accrual + disbursement cycle with its
/// exact dollar postings, the HR-04 idempotent submit replay, the HR-05 cancel mirror (plus the
/// cancel-after-Paid 409), the HR-06 same-period concurrent-submit race resolved by the Block C
/// period-overlap guard, and the HR-03 eligibility skip-and-report - end to end over HTTP
/// against the LIVE dev container.
/// </summary>
/// <remarks>
/// <para><b>The oracle is the DATABASE, not only the response.</b> Every test snapshots the
/// global <c>GLEntry</c> count around the ACT and asserts DELTAS - never absolute totals, which
/// other tests legitimately append to (the FiscalPeriodLock / banking / manufacturing pattern).
/// Serialized through <see cref="LedgerMutatingCollection"/> for the same reason.</para>
/// <para><b>Masters are per-test rows, never shared.</b> Departments, employees, components,
/// structures and assignments use fresh GUIDs (and tag-suffixed employee numbers, which must
/// stay tenant-unique) on every run, deleted in a <c>finally</c> block in FK order, so re-runs
/// stay hermetic. Ledger rows (<c>GLEntry</c>) stay behind like every other posting test leaves
/// them: <c>GLEntry</c> is append-only, and <c>VoucherId</c> carries no FK back to
/// <c>PayrollEntry</c>, so entry/slip rows are removable while their vouchers remain.
/// Shared seeded leaves (5130/2220/2225/2150 from scripts/seed-dev-hr-payroll.sql) are
/// ensured-if-missing by setup and READ-ONLY here.</para>
/// <para><b>Provisioning stays neutral where asserted.</b> Every run nets its 2150 payable to
/// zero (disbursed, or cancelled with the accrual mirror) before cleanup, so no test leaves a
/// payable balance behind; additive balanced GL rows are fine.</para>
/// <para><b>No HR-CRUD endpoints exist (out of scope).</b> Master rows are inserted via direct
/// SQL setup with deterministic per-test GUIDs (the banking/manufacturing per-test-rows
/// pattern); the payroll lifecycle itself (submit/disburse/cancel) always goes through the
/// real API. The four master GET routes (Task 12.5) are exercised by the frontend, not here.</para>
/// </remarks>
[Collection(LedgerMutatingCollection.Name)]
public class PayrollLifecycleApiTests : IClassFixture<ErpApiFactory>
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    private static readonly Guid SalaryExpenseAccountId = Guid.Parse("a0000000-0000-4000-8000-000000005130");
    private static readonly Guid TaxPayableAccountId = Guid.Parse("a0000000-0000-4000-8000-000000002220");
    private static readonly Guid PensionPayableAccountId = Guid.Parse("a0000000-0000-4000-8000-000000002225");
    private static readonly Guid PayrollPayableAccountId = Guid.Parse("a0000000-0000-4000-8000-000000002150");
    private static readonly Guid BankGlAccountId = Guid.Parse("a0000000-0000-4000-8000-000000001110");

    private readonly ErpApiFactory _factory;

    public PayrollLifecycleApiTests(ErpApiFactory factory) => _factory = factory;

    // ------------------------------------------------- HR-01 + HR-02 full cycle live

    /// <summary>
    /// HR-01 + HR-02 live: SQL-seeded Maria (Basic 4000 + Housing 1000, Tax 600, Pension 400)
    /// submits the October run - slip exact (5000/1000/4000), accrual exact (Dr 5130 5000 /
    /// Cr 2220 600 / Cr 2225 400 / Cr 2150 4000) - then disburses via a per-test bank account:
    /// Dr 2150 / Cr bank 4000, payable nets to zero, entry Paid. Exact GL delta is +6.
    /// </summary>
    [Fact]
    public async Task FullCycle_SubmitThenDisburse_PostsExactAccrualAndClearsPayableToZero()
    {
        using var client = CreateClient();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var setup = new PayrollSetup();
        Guid? bankAccountId = null;

        try
        {
            setup.Merge(await SetupStandardRunAsync($"HR-{tag}", "2026-10-01", "2026-10-31"));
            bankAccountId = await CreateBankAccountAsync($"HR Bank {tag}");

            var glBefore = await CountGLEntryAsync();

            using var submit = await PostSubmitAsync(
                client, "2026-10-01", "2026-10-31", "2026-10-31");
            Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
            var submitBody = JsonNode.Parse(await submit.Content.ReadAsStringAsync())!;
            Assert.Equal("Submitted", submitBody["entry"]!["status"]!.GetValue<string>());
            Assert.Equal(1, submitBody["createdSlipCount"]!.GetValue<int>());
            Assert.Equal(5000m, submitBody["entry"]!["totalGrossPay"]!.GetValue<decimal>());
            Assert.Equal(1000m, submitBody["entry"]!["totalDeductions"]!.GetValue<decimal>());
            Assert.Equal(4000m, submitBody["entry"]!["totalNetPay"]!.GetValue<decimal>());
            var entryId = submitBody["entry"]!["id"]!.GetValue<Guid>();

            // HR-01 slip exact, with its four itemized lines.
            using var detail = await client.GetAsync(
                $"/api/v1/payroll-runs/{entryId}?companyId={ErpApiFactory.DevCompanyId}");
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
            var detailBody = JsonNode.Parse(await detail.Content.ReadAsStringAsync())!;
            var slip = Assert.Single(detailBody["slips"]!.AsArray());
            Assert.Equal(5000m, slip!["grossPay"]!.GetValue<decimal>());
            Assert.Equal(1000m, slip!["totalDeductions"]!.GetValue<decimal>());
            Assert.Equal(4000m, slip!["netPay"]!.GetValue<decimal>());
            Assert.Equal(4, slip!["lines"]!.AsArray().Count);

            // HR-02 accrual exact, straight from the ledger.
            var accrual = await ReadLedgerRowsAsync(entryId, onlyLive: true);
            Assert.Equal(4, accrual.Count);
            Assert.Contains(accrual, r => r.Account == SalaryExpenseAccountId && r.Debit == 5000m && r.Credit == 0m);
            Assert.Contains(accrual, r => r.Account == TaxPayableAccountId && r.Debit == 0m && r.Credit == 600m);
            Assert.Contains(accrual, r => r.Account == PensionPayableAccountId && r.Debit == 0m && r.Credit == 400m);
            Assert.Contains(accrual, r => r.Account == PayrollPayableAccountId && r.Debit == 0m && r.Credit == 4000m);

            using var disburse = await PostWithKeyAsync(
                client, HttpMethod.Post,
                $"/api/v1/payroll-runs/{entryId}/disburse?companyId={ErpApiFactory.DevCompanyId}&bankAccountId={bankAccountId}&postingDate=2026-10-31");
            Assert.Equal(HttpStatusCode.OK, disburse.StatusCode);
            var disbursed = JsonNode.Parse(await disburse.Content.ReadAsStringAsync())!;
            Assert.Equal("Paid", disbursed["status"]!.GetValue<string>());

            // The exact HR-02 Phase 2 pair, and the payable nets to exactly zero.
            var all = await ReadLedgerRowsAsync(entryId, onlyLive: false);
            Assert.Equal(6, all.Count);
            var pair = all.Where(r => r.VoucherNo == disbursed["paymentVoucherNo"]!.GetValue<string>()).ToList();
            Assert.Equal(2, pair.Count);
            Assert.Contains(pair, r => r.Account == PayrollPayableAccountId && r.Debit == 4000m && r.Credit == 0m);
            Assert.Equal(0m, await ReadAccountNetAsync(new[] { entryId }, PayrollPayableAccountId));
            Assert.Equal(glBefore + 6, await CountGLEntryAsync());
        }
        finally
        {
            if (bankAccountId is not null)
            {
                await DeleteBankAccountAsync(bankAccountId.Value);
            }

            await DeletePayrollTestDataAsync(setup);
        }
    }

    // ------------------------------------------------------------- HR-04 replay live

    /// <summary>
    /// HR-04 live: submitting the November run twice with the SAME <c>Idempotency-Key</c>
    /// returns 200 with the original body byte-identical, and appends zero <c>GLEntry</c> rows
    /// (and no second entry). The run is cancelled in the finally so the period is released.
    /// </summary>
    [Fact]
    public async Task SubmitReplay_SameKeyTwice_SecondIs200ByteIdenticalWithZeroNewRows()
    {
        using var client = CreateClient();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var setup = new PayrollSetup();

        Guid? entryId = null;
        try
        {
            setup.Merge(await SetupStandardRunAsync($"HR-{tag}", "2026-11-01", "2026-11-30"));

            var key = Guid.NewGuid().ToString("N");
            using var first = await PostSubmitAsync(
                client, "2026-11-01", "2026-11-30", "2026-11-30", key);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            var firstBytes = await first.Content.ReadAsByteArrayAsync();
            entryId = JsonNode.Parse(System.Text.Encoding.UTF8.GetString(firstBytes))!["entry"]!["id"]!.GetValue<Guid>();

            var glBefore = await CountGLEntryAsync();

            using var replay = await PostSubmitAsync(
                client, "2026-11-01", "2026-11-30", "2026-11-30", key);
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
            var replayBytes = await replay.Content.ReadAsByteArrayAsync();

            Assert.Equal(firstBytes, replayBytes);
            Assert.Equal(glBefore, await CountGLEntryAsync());
            Assert.Equal(1, await CountEntriesForPeriodAsync("2026-11-01", "2026-11-30"));
        }
        finally
        {
            if (entryId is not null)
            {
                using var cancelClient = CreateClient();
                using var cancel = await PostWithKeyAsync(
                    cancelClient, HttpMethod.Post,
                    $"/api/v1/payroll-runs/{entryId}/cancel?companyId={ErpApiFactory.DevCompanyId}&postingDate=2026-11-30");
            }

            await DeletePayrollTestDataAsync(setup);
        }
    }

    // ------------------------------------------------- HR-05 cancel/reversal live

    /// <summary>
    /// HR-05 live: submitting the December run then cancelling posts the full mirror (every
    /// accrual line swapped, flagged reversal rows, originals byte-identical), marks the slips
    /// Cancelled and the entry Cancelled. Cancelling a Paid run (March window, disbursed) is a
    /// 409 <c>invalid_status_transition</c> with zero new ledger rows.
    /// </summary>
    [Fact]
    public async Task Cancel_AfterSubmit_MirrorsAccrualAndCancelsSlips_ThenCancelAfterPaidIs409()
    {
        using var client = CreateClient();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var setup = new PayrollSetup();
        Guid? bankAccountId = null;

        try
        {
            setup.Merge(await SetupStandardRunAsync($"HR-{tag}", "2026-12-01", "2026-12-31"));
            bankAccountId = await CreateBankAccountAsync($"HR Bank {tag}");

            using var submit = await PostSubmitAsync(
                client, "2026-12-01", "2026-12-31", "2026-12-31");
            Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
            var entryId = JsonNode.Parse(await submit.Content.ReadAsStringAsync())!["entry"]!["id"]!.GetValue<Guid>();
            var accrual = await ReadLedgerRowsAsync(entryId, onlyLive: true);
            Assert.Equal(4, accrual.Count);

            using var cancel = await PostWithKeyAsync(
                client, HttpMethod.Post,
                $"/api/v1/payroll-runs/{entryId}/cancel?companyId={ErpApiFactory.DevCompanyId}&postingDate=2026-12-31");
            Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
            var cancelled = JsonNode.Parse(await cancel.Content.ReadAsStringAsync())!;
            Assert.Equal("Cancelled", cancelled["status"]!.GetValue<string>());

            // The full mirror: one swapped row per accrual account, flagged reversals.
            var all = await ReadLedgerRowsAsync(entryId, onlyLive: false);
            Assert.Equal(8, all.Count);
            var mirror = all.Where(r => r.Cancelled).ToList();
            Assert.Equal(4, mirror.Count);
            foreach (var original in accrual)
            {
                var reversal = Assert.Single(mirror, m => m.Account == original.Account);
                Assert.Equal(original.Credit, reversal.Debit);
                Assert.Equal(original.Debit, reversal.Credit);
            }

            // Slips Cancelled, entry Cancelled, payable back to zero.
            Assert.All(await ReadSlipStatusesAsync(entryId), s => Assert.Equal("Cancelled", s));
            Assert.Equal(0m, await ReadAccountNetAsync(new[] { entryId }, PayrollPayableAccountId));

            // Cancel-after-Paid: a March run, disbursed, refuses cancellation with 409.
            var paidSetup = await SetupStandardRunAsync($"HP-{tag}", "2027-03-01", "2027-03-31");
            setup.Merge(paidSetup);
            using var paidSubmit = await PostSubmitAsync(
                client, "2027-03-01", "2027-03-31", "2027-03-31");
            Assert.Equal(HttpStatusCode.OK, paidSubmit.StatusCode);
            var paidId = JsonNode.Parse(await paidSubmit.Content.ReadAsStringAsync())!["entry"]!["id"]!.GetValue<Guid>();
            using var disburse = await PostWithKeyAsync(
                client, HttpMethod.Post,
                $"/api/v1/payroll-runs/{paidId}/disburse?companyId={ErpApiFactory.DevCompanyId}&bankAccountId={bankAccountId}&postingDate=2027-03-31");
            Assert.Equal(HttpStatusCode.OK, disburse.StatusCode);

            var glBefore = await CountGLEntryAsync();
            using var cancelPaid = await PostWithKeyAsync(
                client, HttpMethod.Post,
                $"/api/v1/payroll-runs/{paidId}/cancel?companyId={ErpApiFactory.DevCompanyId}&postingDate=2027-03-31");
            Assert.Equal(HttpStatusCode.Conflict, cancelPaid.StatusCode);
            var problem = JsonNode.Parse(await cancelPaid.Content.ReadAsStringAsync())!;
            Assert.Equal("invalid_status_transition", problem["code"]!.GetValue<string>());
            Assert.Equal(glBefore, await CountGLEntryAsync());
        }
        finally
        {
            if (bankAccountId is not null)
            {
                await DeleteBankAccountAsync(bankAccountId.Value);
            }

            await DeletePayrollTestDataAsync(setup);
        }
    }

    // ------------------------------------------------------- HR-06 race live

    /// <summary>
    /// HR-06 live: two concurrent submits for the SAME January-2027 period resolve to exactly
    /// one Submitted entry with slips; the loser is a 409 <c>payroll_period_overlap</c> with
    /// zero partial rows (no Draft leftovers). The (PayrollEntryId, EmployeeId) unique index
    /// cannot catch this shape (separate entries, no shared slip rows) - the Block C overlap
    /// guard is the authority. The winner is cancelled in the finally so the period and the
    /// payable both return to zero.
    /// </summary>
    [Fact]
    public async Task ConcurrentSubmits_SamePeriod_ExactlyOneWinsWithOverlapConflict()
    {
        using var client = CreateClient();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var setup = new PayrollSetup();

        Guid? winnerId = null;
        HttpResponseMessage? first = null;
        HttpResponseMessage? second = null;
        try
        {
            setup.Merge(await SetupStandardRunAsync($"HR-{tag}", "2027-01-01", "2027-01-31"));

            // TRULY concurrent: both submits race for the same period. The numbering
            // UPDLOCK/HOLDLOCK serializes them; the loser observes the winner (409).
            var racerOne = Task.Run(() => PostSubmitAsync(client, "2027-01-01", "2027-01-31", "2027-01-31"));
            var racerTwo = Task.Run(() => PostSubmitAsync(client, "2027-01-01", "2027-01-31", "2027-01-31"));
            first = await racerOne;
            second = await racerTwo;

            var outcomes = new[] { first, second };
            var winners = outcomes.Where(r => r.StatusCode == HttpStatusCode.OK).ToList();
            var losers = outcomes.Where(r => r.StatusCode != HttpStatusCode.OK).ToList();

            var winner = Assert.Single(winners);
            var loser = Assert.Single(losers);
            Assert.Equal(HttpStatusCode.Conflict, loser.StatusCode);
            var problem = JsonNode.Parse(await loser.Content.ReadAsStringAsync())!;
            Assert.Equal("payroll_period_overlap", problem["code"]!.GetValue<string>());

            winnerId = JsonNode.Parse(await winner.Content.ReadAsStringAsync())!["entry"]!["id"]!.GetValue<Guid>();

            // Exactly one entry for the period, Submitted with its slip; zero partial rows.
            Assert.Equal(1, await CountEntriesForPeriodAsync("2027-01-01", "2027-01-31"));
            Assert.Equal("Submitted", await ReadEntryStatusAsync(winnerId.Value));
            Assert.Equal(1, await CountSlipsAsync(winnerId.Value));
        }
        finally
        {
            first?.Dispose();
            second?.Dispose();

            if (winnerId is not null)
            {
                using var cancelClient = CreateClient();
                using var cancel = await PostWithKeyAsync(
                    cancelClient, HttpMethod.Post,
                    $"/api/v1/payroll-runs/{winnerId}/cancel?companyId={ErpApiFactory.DevCompanyId}&postingDate=2027-01-31");
            }

            await DeletePayrollTestDataAsync(setup);
        }
    }

    // ------------------------------------------------------- HR-03 eligibility live

    /// <summary>
    /// HR-03 live: Maria (eligible) prices; the inactive, joined-after-February and
    /// relieved-before-February employees are skipped-and-reported with zero slips.
    /// </summary>
    [Fact]
    public async Task Submit_MixedEligibility_PricesOnlyMariaAndReportsThreeSkips()
    {
        using var client = CreateClient();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var setup = new PayrollSetup();
        Guid? entryId = null;

        try
        {
            setup.Merge(await SetupStandardRunAsync($"HR-{tag}", "2027-02-01", "2027-02-28"));

            // Three ineligible colleagues on the same standard structure: inactive status,
            // joined after the period, relieved before the period.
            var inactiveId = await InsertEmployeeAsync(setup, $"HR-{tag}-INACT", status: "Inactive", joined: "2026-01-05", relieved: null);
            await InsertAssignmentAsync(setup, inactiveId, setup.StructureId, "2026-01-01", null);
            var lateId = await InsertEmployeeAsync(setup, $"HR-{tag}-LATE", status: "Active", joined: "2027-03-01", relieved: null);
            await InsertAssignmentAsync(setup, lateId, setup.StructureId, "2027-03-01", null);
            var leftId = await InsertEmployeeAsync(setup, $"HR-{tag}-LEFT", status: "Active", joined: "2026-01-05", relieved: "2027-01-15");
            await InsertAssignmentAsync(setup, leftId, setup.StructureId, "2026-01-01", null);

            using var submit = await PostSubmitAsync(
                client, "2027-02-01", "2027-02-28", "2027-02-28");
            Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
            var body = JsonNode.Parse(await submit.Content.ReadAsStringAsync())!;
            Assert.Equal(1, body["createdSlipCount"]!.GetValue<int>());
            entryId = body["entry"]!["id"]!.GetValue<Guid>();

            var skipped = body["skipped"]!.AsArray();
            Assert.Equal(3, skipped.Count);
            var skippedIds = skipped.Select(n => n!["employeeId"]!.GetValue<Guid>()).ToHashSet();
            Assert.Contains(inactiveId, skippedIds);
            Assert.Contains(lateId, skippedIds);
            Assert.Contains(leftId, skippedIds);
            Assert.DoesNotContain(setup.MariaId, skippedIds);

            // Zero slips for the excluded; Maria holds the single slip.
            Assert.Equal(setup.MariaId, Assert.Single(await ReadSlipEmployeeIdsAsync(entryId.Value)));
            Assert.Equal(0, await CountSlipsForEmployeesAsync(new[] { inactiveId, lateId, leftId }));
        }
        finally
        {
            if (entryId is not null)
            {
                using var cancelClient = CreateClient();
                using var cancel = await PostWithKeyAsync(
                    cancelClient, HttpMethod.Post,
                    $"/api/v1/payroll-runs/{entryId}/cancel?companyId={ErpApiFactory.DevCompanyId}&postingDate=2027-02-28");
            }

            await DeletePayrollTestDataAsync(setup);
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

    private static Task<HttpResponseMessage> PostSubmitAsync(
        HttpClient client, string start, string end, string postingDate, string? key = null)
    {
        var payload = new
        {
            companyId = ErpApiFactory.DevCompanyId,
            startDate = start,
            endDate = end,
            postingDate,
            paymentDayOverrides = (object?)null,
        };
        return PostWithKeyAsync(client, HttpMethod.Post, "/api/v1/payroll-runs/submit", payload, key);
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

    /// <summary>Per-test master holder: every row this run created, for FK-order cleanup.</summary>
    private sealed class PayrollSetup
    {
        public List<Guid> DepartmentIds { get; } = new();
        public List<Guid> EmployeeIds { get; } = new();
        public List<Guid> ComponentIds { get; } = new();
        public List<Guid> StructureIds { get; } = new();
        public List<Guid> AssignmentIds { get; } = new();
        public List<Guid> EntryIds { get; } = new();
        public Guid MariaId { get; set; }
        public Guid StructureId { get; set; }

        public void Merge(PayrollSetup other)
        {
            DepartmentIds.AddRange(other.DepartmentIds);
            EmployeeIds.AddRange(other.EmployeeIds);
            ComponentIds.AddRange(other.ComponentIds);
            StructureIds.AddRange(other.StructureIds);
            AssignmentIds.AddRange(other.AssignmentIds);
            EntryIds.AddRange(other.EntryIds);
            MariaId = other.MariaId;
            StructureId = other.StructureId;
        }
    }

    /// <summary>
    /// Seeds one department, Maria (eligible: Active, joined 2026-01-05), the four standard
    /// components on the exact seeded GLs (earnings to 5130, tax to 2220, pension to 2225),
    /// the standard structure (4000 + 1000 / 600 / 400) and Maria's open assignment. Also
    /// ensures the four shared GL leaves exist and the company points its payable code at
    /// 2150 (restored by <see cref="DeletePayrollTestDataAsync"/> only for the company code;
    /// the ensured leaves are shared-seed idempotent, like seed-dev-hr-payroll.sql itself).
    /// </summary>
    private static async Task<PayrollSetup> SetupStandardRunAsync(string numberPrefix, string assignFrom, string assignTo)
    {
        var setup = new PayrollSetup();
        await EnsurePayrollGlSeedAsync();

        var departmentId = Guid.NewGuid();
        await ExecuteAsync(
            "INSERT INTO dbo.Department (Id, TenantId, CompanyId, DepartmentName, ParentDepartmentId, IsActive) "
            + "VALUES (@Id, @TenantId, @CompanyId, N'HR Test Dept', NULL, 1);",
            ("@Id", departmentId),
            ("@TenantId", ErpApiFactory.DevTenantId),
            ("@CompanyId", ErpApiFactory.DevCompanyId));
        setup.DepartmentIds.Add(departmentId);

        var mariaId = await InsertEmployeeAsync(setup, $"{numberPrefix}-MARIA", "Active", "2026-01-05", null, departmentId);
        setup.MariaId = mariaId;

        var basicId = await InsertComponentAsync(setup, "Basic Salary", "Earning", SalaryExpenseAccountId);
        var housingId = await InsertComponentAsync(setup, "Housing Allowance", "Earning", SalaryExpenseAccountId);
        var taxId = await InsertComponentAsync(setup, "Income Tax", "Deduction", TaxPayableAccountId);
        var pensionId = await InsertComponentAsync(setup, "Pension", "Deduction", PensionPayableAccountId);

        var structureId = Guid.NewGuid();
        await ExecuteAsync(
            "INSERT INTO dbo.SalaryStructure (Id, TenantId, CompanyId, StructureName, IsActive) "
            + "VALUES (@Id, @TenantId, @CompanyId, N'HR Standard', 1);",
            ("@Id", structureId),
            ("@TenantId", ErpApiFactory.DevTenantId),
            ("@CompanyId", ErpApiFactory.DevCompanyId));
        setup.StructureIds.Add(structureId);
        setup.StructureId = structureId;

        await InsertStructureLineAsync(setup, structureId, basicId, 4000m);
        await InsertStructureLineAsync(setup, structureId, housingId, 1000m);
        await InsertStructureLineAsync(setup, structureId, taxId, 600m);
        await InsertStructureLineAsync(setup, structureId, pensionId, 400m);

        await InsertAssignmentAsync(setup, mariaId, structureId, assignFrom, assignTo);
        return setup;
    }

    private static async Task<Guid> InsertEmployeeAsync(
        PayrollSetup setup, string number, string status, string joined, string? relieved, Guid? departmentId = null)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            "INSERT INTO dbo.Employee (Id, TenantId, CompanyId, EmployeeNumber, FirstName, LastName, WorkEmail, "
            + "DepartmentId, DesignationId, DateOfJoining, DateOfRelieving, SalaryMode, BankName, BankAccountNumber, Status, IsActive) "
            + "VALUES (@Id, @TenantId, @CompanyId, @Number, N'Maria', N'Test', @Email, @DeptId, NULL, "
            + "@Joined, @Relieved, N'Bank', N'HR Bank', N'004921', @Status, 1);",
            ("@Id", id),
            ("@TenantId", ErpApiFactory.DevTenantId),
            ("@CompanyId", ErpApiFactory.DevCompanyId),
            ("@Number", number),
            ("@Email", $"{number}@hr.test"),
            ("@DeptId", (object?)departmentId ?? DBNull.Value),
            ("@Joined", DateOnly.Parse(joined)),
            ("@Relieved", relieved is null ? DBNull.Value : (object)DateOnly.Parse(relieved)),
            ("@Status", status));
        setup.EmployeeIds.Add(id);
        return id;
    }

    private static async Task<Guid> InsertComponentAsync(PayrollSetup setup, string name, string type, Guid glAccountId)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            "INSERT INTO dbo.SalaryComponent (Id, TenantId, CompanyId, ComponentName, ComponentType, "
            + "DependsOnPaymentDays, IsTaxApplicable, DefaultGLAccountId, IsActive) "
            + "VALUES (@Id, @TenantId, @CompanyId, @Name, @Type, 0, 1, @GlId, 1);",
            ("@Id", id),
            ("@TenantId", ErpApiFactory.DevTenantId),
            ("@CompanyId", ErpApiFactory.DevCompanyId),
            ("@Name", name),
            ("@Type", type),
            ("@GlId", glAccountId));
        setup.ComponentIds.Add(id);
        return id;
    }

    private static async Task InsertStructureLineAsync(PayrollSetup setup, Guid structureId, Guid componentId, decimal amount)
    {
        await ExecuteAsync(
            "INSERT INTO dbo.SalaryStructureLine (Id, StructureId, ComponentId, Amount, PercentageOfBase) "
            + "VALUES (@Id, @StructureId, @ComponentId, @Amount, NULL);",
            ("@Id", Guid.NewGuid()),
            ("@StructureId", structureId),
            ("@ComponentId", componentId),
            ("@Amount", amount));
    }

    private static async Task InsertAssignmentAsync(PayrollSetup setup, Guid employeeId, Guid structureId, string from, string? to)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            "INSERT INTO dbo.SalaryStructureAssignment (Id, TenantId, CompanyId, EmployeeId, StructureId, EffectiveFrom, EffectiveTo, IsActive) "
            + "VALUES (@Id, @TenantId, @CompanyId, @EmployeeId, @StructureId, @From, @To, 1);",
            ("@Id", id),
            ("@TenantId", ErpApiFactory.DevTenantId),
            ("@CompanyId", ErpApiFactory.DevCompanyId),
            ("@EmployeeId", employeeId),
            ("@StructureId", structureId),
            ("@From", DateOnly.Parse(from)),
            ("@To", to is null ? DBNull.Value : (object)DateOnly.Parse(to)));
        setup.AssignmentIds.Add(id);
    }

    /// <summary>
    /// Idempotent shared-seed guard (mirrors scripts/seed-dev-hr-payroll.sql): the four GL
    /// leaves plus the dev company's payable code. The previous payable code is captured so
    /// cleanup can restore it; the leaves themselves are shared-seed rows and stay.
    /// </summary>
    private static string? _previousPayableCode;
    private static int _payableCodeCaptured;

    private static async Task EnsurePayrollGlSeedAsync()
    {
        await ExecuteAsync(
            "IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = @Id) "
            + "INSERT INTO dbo.Account (Id, TenantId, CompanyId, AccountCode, AccountName, RootType, IsGroup, ParentAccountId, Currency, IsActive) "
            + "VALUES (@Id, @TenantId, @CompanyId, N'5130', N'Salaries and Wages Expense', N'Expense', 0, "
            + "(SELECT Id FROM dbo.Account WHERE TenantId = @TenantId AND CompanyId = @CompanyId AND AccountCode = N'5000'), N'USD', 1);",
            ("@Id", SalaryExpenseAccountId),
            ("@TenantId", ErpApiFactory.DevTenantId),
            ("@CompanyId", ErpApiFactory.DevCompanyId));

        await ExecuteAsync(
            "IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = @Id) "
            + "INSERT INTO dbo.Account (Id, TenantId, CompanyId, AccountCode, AccountName, RootType, IsGroup, ParentAccountId, Currency, IsActive) "
            + "VALUES (@Id, @TenantId, @CompanyId, N'2220', N'Income Tax Payable', N'Liability', 0, "
            + "(SELECT Id FROM dbo.Account WHERE TenantId = @TenantId AND CompanyId = @CompanyId AND AccountCode = N'2000'), N'USD', 1);",
            ("@Id", TaxPayableAccountId),
            ("@TenantId", ErpApiFactory.DevTenantId),
            ("@CompanyId", ErpApiFactory.DevCompanyId));

        await ExecuteAsync(
            "IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = @Id) "
            + "INSERT INTO dbo.Account (Id, TenantId, CompanyId, AccountCode, AccountName, RootType, IsGroup, ParentAccountId, Currency, IsActive) "
            + "VALUES (@Id, @TenantId, @CompanyId, N'2225', N'Social Security Payable', N'Liability', 0, "
            + "(SELECT Id FROM dbo.Account WHERE TenantId = @TenantId AND CompanyId = @CompanyId AND AccountCode = N'2000'), N'USD', 1);",
            ("@Id", PensionPayableAccountId),
            ("@TenantId", ErpApiFactory.DevTenantId),
            ("@CompanyId", ErpApiFactory.DevCompanyId));

        await ExecuteAsync(
            "IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = @Id) "
            + "INSERT INTO dbo.Account (Id, TenantId, CompanyId, AccountCode, AccountName, RootType, IsGroup, ParentAccountId, Currency, IsActive) "
            + "VALUES (@Id, @TenantId, @CompanyId, N'2150', N'Payroll Payable', N'Liability', 0, "
            + "(SELECT Id FROM dbo.Account WHERE TenantId = @TenantId AND CompanyId = @CompanyId AND AccountCode = N'2000'), N'USD', 1);",
            ("@Id", PayrollPayableAccountId),
            ("@TenantId", ErpApiFactory.DevTenantId),
            ("@CompanyId", ErpApiFactory.DevCompanyId));

        if (System.Threading.Interlocked.CompareExchange(ref _payableCodeCaptured, 1, 0) == 0)
        {
            _previousPayableCode = await ReadCompanyPayableCodeAsync();
        }

        await ExecuteAsync(
            "UPDATE dbo.Company SET PayrollPayableAccountCode = N'2150' WHERE Id = @CompanyId;",
            ("@CompanyId", ErpApiFactory.DevCompanyId));
    }

    private static async Task<string?> ReadCompanyPayableCodeAsync()
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT PayrollPayableAccountCode FROM dbo.Company WHERE Id = @CompanyId;", connection);
        command.Parameters.AddWithValue("@CompanyId", ErpApiFactory.DevCompanyId);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? null : (string)value;
    }

    private static async Task<Guid> CreateBankAccountAsync(string name)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            "INSERT INTO dbo.BankAccount (Id, TenantId, CompanyId, AccountName, BankName, AccountNumber, Currency, GLAccountId, LastReconciledBalance, IsActive) "
            + "VALUES (@Id, @TenantId, @CompanyId, @Name, N'HR Test Bank', @Number, N'USD', @GlAccountId, 0, 1);",
            ("@Id", id),
            ("@TenantId", ErpApiFactory.DevTenantId),
            ("@CompanyId", ErpApiFactory.DevCompanyId),
            ("@Name", name),
            ("@Number", $"HR-{id:N}"),
            ("@GlAccountId", BankGlAccountId));
        return id;
    }

    /// <summary>Deletes a test bank account (no staging rows exist for payroll accounts).</summary>
    private static async Task DeleteBankAccountAsync(Guid bankAccountId)
    {
        await ExecuteAsync(
            "DELETE FROM dbo.BankAccount WHERE Id = @Id;",
            ("@Id", bankAccountId));
    }

    /// <summary>
    /// Deletes every per-test row in FK order (lines -&gt; slips -&gt; entries -&gt; assignments
    /// -&gt; structure lines -&gt; structures -&gt; components -&gt; employees -&gt; departments)
    /// and restores the company's previous payable code. GL vouchers stay (append-only).
    /// Entry ids are re-read from the period-agnostic slip/entry link: entries are tracked by
    /// re-querying slips of the setup employees, so even the race loser's zero rows and the
    /// winner's entry are covered without the test naming them.
    /// </summary>
    private static async Task DeletePayrollTestDataAsync(PayrollSetup setup)
    {
        var entryIds = await ReadEntryIdsForEmployeesAsync(setup.EmployeeIds);
        foreach (var id in entryIds.Where(id => !setup.EntryIds.Contains(id)))
        {
            setup.EntryIds.Add(id);
        }

        await ExecuteIdsAsync(
            "DELETE FROM dbo.SalarySlipLine WHERE SlipId IN (SELECT Id FROM dbo.SalarySlip WHERE PayrollEntryId IN (SELECT value FROM OPENJSON(@Ids)));",
            setup.EntryIds);
        await ExecuteIdsAsync(
            "DELETE FROM dbo.SalarySlip WHERE PayrollEntryId IN (SELECT value FROM OPENJSON(@Ids));",
            setup.EntryIds);
        await ExecuteIdsAsync(
            "DELETE FROM dbo.PayrollEntry WHERE Id IN (SELECT value FROM OPENJSON(@Ids));",
            setup.EntryIds);
        await ExecuteIdsAsync(
            "DELETE FROM dbo.SalaryStructureAssignment WHERE Id IN (SELECT value FROM OPENJSON(@Ids));",
            setup.AssignmentIds);
        await ExecuteIdsAsync(
            "DELETE FROM dbo.SalaryStructureLine WHERE StructureId IN (SELECT value FROM OPENJSON(@Ids));",
            setup.StructureIds);
        await ExecuteIdsAsync(
            "DELETE FROM dbo.SalaryStructure WHERE Id IN (SELECT value FROM OPENJSON(@Ids));",
            setup.StructureIds);
        await ExecuteIdsAsync(
            "DELETE FROM dbo.SalaryComponent WHERE Id IN (SELECT value FROM OPENJSON(@Ids));",
            setup.ComponentIds);
        await ExecuteIdsAsync(
            "DELETE FROM dbo.Employee WHERE Id IN (SELECT value FROM OPENJSON(@Ids));",
            setup.EmployeeIds);
        await ExecuteIdsAsync(
            "DELETE FROM dbo.Department WHERE Id IN (SELECT value FROM OPENJSON(@Ids));",
            setup.DepartmentIds);

        if (_previousPayableCode is null)
        {
            await ExecuteAsync(
                "UPDATE dbo.Company SET PayrollPayableAccountCode = NULL WHERE Id = @CompanyId;",
                ("@CompanyId", ErpApiFactory.DevCompanyId));
        }
        else
        {
            await ExecuteAsync(
                "UPDATE dbo.Company SET PayrollPayableAccountCode = @Code WHERE Id = @CompanyId;",
                ("@Code", _previousPayableCode),
                ("@CompanyId", ErpApiFactory.DevCompanyId));
        }
    }

    // ----------------------------------------------------------------------------- SQL oracles

    private static async Task ExecuteAsync(string sql, params (string Name, object? Value)[] parameters)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            // Bind DateOnly as a true date value so comparisons never depend on session format
            // (the FinancialReportsApiTests AddDateParameter precedent).
            var bound = value is DateOnly date ? date.ToDateTime(TimeOnly.MinValue) : value ?? DBNull.Value;
            command.Parameters.AddWithValue(name, bound);
        }

        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteIdsAsync(string sql, IEnumerable<Guid> ids)
    {
        var list = ids.ToList();
        if (list.Count == 0)
        {
            return;
        }

        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue(
            "@Ids", System.Text.Json.JsonSerializer.Serialize(list.Select(id => id.ToString())));
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Row count of the whole ledger - the "nothing was written" oracle.</summary>
    private static async Task<int> CountGLEntryAsync()
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.GLEntry;", connection);
        return checked((int)(long)(await command.ExecuteScalarAsync())!);
    }

    private sealed record LedgerRow(Guid Account, decimal Debit, decimal Credit, string VoucherNo, bool Cancelled);

    private static async Task<IReadOnlyList<LedgerRow>> ReadLedgerRowsAsync(Guid entryId, bool onlyLive)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT AccountId, Debit, Credit, VoucherNo, IsCancelled FROM dbo.GLEntry "
            + "WHERE VoucherId = @Id AND VoucherType = N'Payroll'"
            + (onlyLive ? " AND IsCancelled = 0" : string.Empty)
            + " ORDER BY Id;",
            connection);
        command.Parameters.AddWithValue("@Id", entryId);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<LedgerRow>();
        while (await reader.ReadAsync())
        {
            rows.Add(new LedgerRow(
                reader.GetGuid(0), reader.GetDecimal(1), reader.GetDecimal(2),
                reader.GetString(3), reader.GetBoolean(4)));
        }

        return rows;
    }

    /// <summary>Net (debit - credit) of one account across the given entry vouchers.</summary>
    private static async Task<decimal> ReadAccountNetAsync(IEnumerable<Guid> entryIds, Guid accountId)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT ISNULL(SUM(Debit), 0) - ISNULL(SUM(Credit), 0) FROM dbo.GLEntry "
            + "WHERE VoucherId IN (SELECT value FROM OPENJSON(@Ids)) AND AccountId = @AccountId;",
            connection);
        command.Parameters.AddWithValue(
            "@Ids", System.Text.Json.JsonSerializer.Serialize(entryIds.Select(id => id.ToString())));
        command.Parameters.AddWithValue("@AccountId", accountId);
        return (decimal)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<int> CountEntriesForPeriodAsync(string start, string end)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT COUNT_BIG(*) FROM dbo.PayrollEntry WHERE CompanyId = @CompanyId "
            + "AND StartDate = @Start AND EndDate = @End;",
            connection);
        command.Parameters.AddWithValue("@CompanyId", ErpApiFactory.DevCompanyId);
        command.Parameters.AddWithValue("@Start", DateOnly.Parse(start));
        command.Parameters.AddWithValue("@End", DateOnly.Parse(end));
        return checked((int)(long)(await command.ExecuteScalarAsync())!);
    }

    private static async Task<string> ReadEntryStatusAsync(Guid entryId)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT Status FROM dbo.PayrollEntry WHERE Id = @Id;", connection);
        command.Parameters.AddWithValue("@Id", entryId);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<int> CountSlipsAsync(Guid entryId)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT COUNT_BIG(*) FROM dbo.SalarySlip WHERE PayrollEntryId = @Id;", connection);
        command.Parameters.AddWithValue("@Id", entryId);
        return checked((int)(long)(await command.ExecuteScalarAsync())!);
    }

    private static async Task<IReadOnlyList<string>> ReadSlipStatusesAsync(Guid entryId)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT Status FROM dbo.SalarySlip WHERE PayrollEntryId = @Id;", connection);
        command.Parameters.AddWithValue("@Id", entryId);
        await using var reader = await command.ExecuteReaderAsync();
        var statuses = new List<string>();
        while (await reader.ReadAsync())
        {
            statuses.Add(reader.GetString(0));
        }

        return statuses;
    }

    private static async Task<IReadOnlyList<Guid>> ReadSlipEmployeeIdsAsync(Guid entryId)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT EmployeeId FROM dbo.SalarySlip WHERE PayrollEntryId = @Id;", connection);
        command.Parameters.AddWithValue("@Id", entryId);
        await using var reader = await command.ExecuteReaderAsync();
        var ids = new List<Guid>();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }

    private static async Task<int> CountSlipsForEmployeesAsync(IEnumerable<Guid> employeeIds)
    {
        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT COUNT_BIG(*) FROM dbo.SalarySlip WHERE EmployeeId IN (SELECT value FROM OPENJSON(@Ids));",
            connection);
        command.Parameters.AddWithValue(
            "@Ids", System.Text.Json.JsonSerializer.Serialize(employeeIds.Select(id => id.ToString())));
        return checked((int)(long)(await command.ExecuteScalarAsync())!);
    }

    private static async Task<IReadOnlyList<Guid>> ReadEntryIdsForEmployeesAsync(IEnumerable<Guid> employeeIds)
    {
        var list = employeeIds.ToList();
        if (list.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT DISTINCT PayrollEntryId FROM dbo.SalarySlip WHERE EmployeeId IN (SELECT value FROM OPENJSON(@Ids));",
            connection);
        command.Parameters.AddWithValue(
            "@Ids", System.Text.Json.JsonSerializer.Serialize(list.Select(id => id.ToString())));
        await using var reader = await command.ExecuteReaderAsync();
        var ids = new List<Guid>();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }
}
