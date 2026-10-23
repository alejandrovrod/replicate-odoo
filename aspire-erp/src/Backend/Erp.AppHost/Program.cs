using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = DistributedApplication.CreateBuilder(args);

// Strong SQL Server sa password (upper, lower, digit, symbol, >= 8 chars).
const string SqlSaPassword = "Erp_Dev_P@ssw0rd!2026";

// Aspire reports a resource's health as `null` when no health check is registered, and `null`
// does not satisfy the Phase 0 DoD, so both containers get an explicit TCP-connect check.
builder.Services.AddHealthChecks()
    //.AddCheck<SqlServerTcpHealthCheck>("sqlserver-tcp")
    .AddCheck<RedisTcpHealthCheck>("redis-tcp");

// Base de datos externa/remota configurada en appsettings o variables de entorno
var sqlDb = builder.AddConnectionString("erp-db");

var redis = builder
    .AddRedis("erp-redis", port: HostPorts.Redis)
    .WithHostPort(HostPorts.Redis)
    .WithHealthCheck("redis-tcp");

// Composition root of the solution. Scope decision (Phase 0): the React frontend is intentionally
// NOT registered here - Task 0.3's DoD is a standalone `npm run build` + dev server.
builder.AddProject<Projects.Erp_Api>("erp-api")
    .WithReference(sqlDb)
    .WithReference(redis)
    .WaitFor(redis);

builder.Build().Run();

/// <summary>Fixed host ports so Phase 1+ connection strings stay stable.</summary>
internal static class HostPorts
{
    public const int SqlServer = 1433;
    public const int Redis = 6379;
}

/// <summary>Base TCP-connect health check used by the AppHost for container resources.</summary>
/// <remarks>
/// IPv4 loopback is used explicitly because Docker publishes these ports bound to 127.0.0.1 only,
/// while "localhost" may resolve to ::1 first on dual-stack Windows.
/// </remarks>
internal abstract class TcpConnectHealthCheck(string host, int port) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));

            await client.ConnectAsync(host, port, timeout.Token);
            return client.Connected
                ? HealthCheckResult.Healthy($"{host}:{port} is accepting TCP connections.")
                : HealthCheckResult.Unhealthy($"{host}:{port} did not accept the TCP connection.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"{host}:{port} is unreachable.", ex);
        }
    }
}

internal sealed class SqlServerTcpHealthCheck() : TcpConnectHealthCheck("127.0.0.1", HostPorts.SqlServer);

internal sealed class RedisTcpHealthCheck() : TcpConnectHealthCheck("127.0.0.1", HostPorts.Redis);
