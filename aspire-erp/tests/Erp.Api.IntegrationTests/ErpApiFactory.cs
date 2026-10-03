using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;

namespace Erp.Api.IntegrationTests;

/// <summary>
/// Boots the real Erp.Api entry point (top-level `Program`, exposed as a public partial class
/// for exactly this purpose) through <see cref="WebApplicationFactory{TEntryPoint}"/> - Task 1.3
/// acceptance: "HTTP integration tests return 200 OK with a tenant-scoped account tree".
/// </summary>
/// <remarks>
/// <para><b>DB:</b> tests run against the LIVE dev SQL container (127.0.0.1,1433 / erp-db), the
/// same database `dotnet ef database update` and the dev seed scripts use, wired in through the
/// <c>ConnectionStrings__erp-db</c> environment variable that Program.cs reads. Task 1.3 needs
/// the REAL write path (POST -> EF insert -> UQ_Account_Tenant_Company_Code -> tree read model),
/// so a mocked DbContext would prove nothing; Phase 1 has no separate integration-test database
/// and the container is the documented dev dependency.</para>
///
/// <para><b>Auth:</b> Erp.Api registers no authentication scheme yet (Program.cs). The
/// "TenantMember" policy is a requirement-only policy: TenantMemberHandler succeeds whenever
/// TenantResolutionMiddleware resolved a tenant from the <c>X-Tenant-ID</c> header - which is
/// why the working e2e scripts send only that header while logs show UserId "unauthenticated".
/// The tests mirror that exact contract (X-Tenant-ID only); security is NOT disabled, and a
/// request WITHOUT the header is still rejected by the middleware with RFC 7807 400.</para>
///
/// <para><b>Cleanup:</b> every account a test creates uses the `IT-` code prefix; Dispose()
/// deletes those rows so re-runs stay idempotent even after a crashed run.</para>
///
/// <para><b>One fixture per test class:</b> xunit builds a fixture per class and runs classes
/// concurrently, so several factories live side by side. The connection-string env var is
/// therefore ref-counted (see <see cref="_liveFactories"/>) instead of being saved/restored per
/// instance - a per-instance restore would yank the value away from a sibling factory that has
/// not built its host yet, and Program.cs throws when it is missing.</para>
/// </remarks>
public sealed class ErpApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Dev tenant of scripts/seed-dev-coa.sql (the only tenant with a seeded COA).</summary>
    public static readonly Guid DevTenantId = Guid.Parse("11111111-1111-4111-8111-111111111111");

    /// <summary>Dev company of scripts/seed-dev-coa.sql (owns the 16 seeded accounts).</summary>
    public static readonly Guid DevCompanyId = Guid.Parse("22222222-2222-4222-8222-222222222222");

    /// <summary>A tenant that owns NO accounts - proves the tree read is tenant-scoped.</summary>
    public static readonly Guid ForeignTenantId = Guid.Parse("99999999-9999-4999-8999-999999999999");

    /// <summary>Same connection string as `dotnet ef database update` (see class remarks).</summary>
    public const string DevConnectionString =
        "Server=127.0.0.1,1433;Database=erp-db;User Id=sa;Password=Erp_Dev_P@ssw0rd!2026;TrustServerCertificate=True";

    private const string ConnectionStringEnvVar = "ConnectionStrings__erp-db";

    /// <summary>
    /// Guards the process-wide environment variable: xunit instantiates ONE fixture per test
    /// class and runs classes in parallel, so several factories can be alive at once.
    /// </summary>
    private static readonly object ConnectionStringGate = new();

    /// <summary>Number of factories currently alive - only the LAST one restores the variable.</summary>
    private static int _liveFactories;

    /// <summary>
    /// Value of <c>ConnectionStrings__erp-db</c> as it was before the FIRST factory touched it.
    /// appsettings.json carries no erp-db entry (Program.cs reads only the environment variable
    /// and throws when it is missing), so restoring it too early - while another factory is still
    /// building its host - would make that factory crash instead of boot.
    /// </summary>
    private static string? _originalConnectionString;

    private bool _disposed;

    public ErpApiFactory()
    {
        // Program.cs reads ConnectionStrings:erp-db when the host is built (lazily, on the first
        // CreateClient()); environment variables win over appsettings.json and use the `__`
        // separator for the colon. Every factory sets the SAME dev value, so the only operation
        // that needs coordinating is the restore: it must happen when the last live factory goes
        // away, never while a sibling fixture is still serving requests.
        lock (ConnectionStringGate)
        {
            if (_liveFactories == 0)
            {
                _originalConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVar);
            }

            _liveFactories++;
            Environment.SetEnvironmentVariable(ConnectionStringEnvVar, DevConnectionString);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // The dev container/e2e scripts run Erp.Api in Development (OpenAPI mapping and the
        // Aspire /health + /alive endpoints are Development-only).
        builder.UseEnvironment("Development");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && !_disposed)
        {
            _disposed = true;

            lock (ConnectionStringGate)
            {
                _liveFactories = Math.Max(0, _liveFactories - 1);

                if (_liveFactories == 0
                    && Environment.GetEnvironmentVariable(ConnectionStringEnvVar) == DevConnectionString)
                {
                    // Only the last live factory hands the variable back - and only if nobody
                    // else overwrote it meanwhile (never clobber an operator's own value).
                    Environment.SetEnvironmentVariable(ConnectionStringEnvVar, _originalConnectionString);
                }
            }

            DeleteCreatedAccounts();
        }
    }

    /// <summary>
    /// Removes the `IT-` accounts created during the run (fail loudly if the container is gone -
    /// a silent skip would let stale rows accumulate and break idempotency).
    /// </summary>
    private static void DeleteCreatedAccounts()
    {
        using var connection = new SqlConnection(DevConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            "DELETE FROM dbo.Account WHERE TenantId = @TenantId AND AccountCode LIKE N'IT-%';";
        command.Parameters.AddWithValue("@TenantId", DevTenantId);
        command.ExecuteNonQuery();
    }
}

