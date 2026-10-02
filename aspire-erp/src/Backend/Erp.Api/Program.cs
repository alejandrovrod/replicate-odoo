using System.Text.Json.Serialization;
using Erp.Api.Authorization;
using Erp.Api.Middleware;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Accounts.Commands;
using Erp.Application.Features.Accounts.Queries;
using Erp.Domain.Repositories;
using Erp.Infrastructure.Data;
using Erp.Infrastructure.Data.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Aspire service defaults: OpenTelemetry export, health checks, service discovery,
// and HTTP resilience pipelines (Constitution Article V.3).
builder.AddServiceDefaults();

// Add services to the container.
// Enums serialize as their NVARCHAR names ("Asset", ...) per plan.md §7.3 so the API contract
// matches the stored value and the ubiquitous language.
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Multi-tenancy composition root (Constitution Article II.2): the scoped tenant provider is
// injected into AppDbContext, which is wired to the Aspire-injected ConnectionStrings:erp-db.
builder.Services.AddScoped<ITenantProvider, TenantProvider>();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("erp-db")
            ?? throw new InvalidOperationException(
                "Connection string 'erp-db' is missing. Run the app through Erp.AppHost "
                + "(sql.AddDatabase(\"erp-db\")) or set ConnectionStrings__erp-db.")));

// Hand-rolled CQRS (decision C2): ISender dispatches to the feature handlers below. No MediatR,
// no extra NuGet packages - Erp.Application still depends only on Erp.Domain (Constitution I.3).
builder.Services.AddScoped<ISender, Sender>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<ICommandHandler<CreateAccountCommand, Result<AccountDto>>, CreateAccountCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetAccountTreeQuery, IReadOnlyList<AccountTreeNodeDto>>, GetAccountTreeQueryHandler>();

// Constitution Article VI.1: the TenantMember policy. Phase 2 has NO authentication task, so the
// requirement is "TenantResolutionMiddleware resolved a tenant" (TenantMemberHandler). Add
// RequireAuthenticatedUser() + membership checks when bearer auth lands; a requirement-only policy
// needs no AddAuthentication registration and adds no auth packages (verified empirically).
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TenantMember", policy => policy.AddRequirements(new TenantMemberRequirement()));
});
builder.Services.AddScoped<IAuthorizationHandler, TenantMemberHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Tenant pipeline first: resolution must run before the logging scope opens, because the scope
// reads the already-resolved tenant (plan.md §2.1 request lifecycle).
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseMiddleware<TenantLoggingScopeMiddleware>();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// Aspire health check endpoints (/health and /alive) - Development only.
app.MapDefaultEndpoints();

app.Run();
