using System.Text.Json.Serialization;
using Erp.Api.Authorization;
using Erp.Api.Filters;
using Erp.Api.Middleware;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Accounts.Commands;
using Erp.Application.Features.Accounts.Queries;
using Erp.Application.Features.Items.Commands;
using Erp.Application.Features.Items.Queries;
using Erp.Application.Features.Stock.Commands;
using Erp.Application.Features.Stock.Queries;
using Erp.Application.Features.Warehouses.Commands;
using Erp.Application.Features.Warehouses.Queries;
using Erp.Application.Services;
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

// Stock & Inventory (Tasks 3.1-3.3): masters, the perpetual-inventory posting engine and the
// Article VI.4 idempotency filter. Repositories stay in Erp.Infrastructure, handlers in
// Erp.Application - only the composition root knows both (decision C2).
builder.Services.AddScoped<ICompanyRepository, CompanyRepository>();
builder.Services.AddScoped<IItemRepository, ItemRepository>();
builder.Services.AddScoped<IUomRepository, UomRepository>();
builder.Services.AddScoped<IWarehouseRepository, WarehouseRepository>();
builder.Services.AddScoped<IStockRepository, StockRepository>();
builder.Services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();
builder.Services.AddScoped<IStockPostingService, StockPostingService>();

builder.Services.AddScoped<ICommandHandler<CreateItemCommand, Result<ItemDto>>, CreateItemCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetItemsQuery, IReadOnlyList<ItemDto>>, GetItemsQueryHandler>();
builder.Services.AddScoped<ICommandHandler<CreateWarehouseCommand, Result<WarehouseDto>>, CreateWarehouseCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetWarehousesQuery, IReadOnlyList<WarehouseTreeNodeDto>>, GetWarehousesQueryHandler>();
builder.Services.AddScoped<ICommandHandler<CreateStockEntryCommand, Result<StockEntryPostingDto>>, CreateStockEntryCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetStockEntriesQuery, IReadOnlyList<StockEntryDto>>, GetStockEntriesQueryHandler>();

// [IdempotencyKeyRequired] is a ServiceFilterAttribute, so the filter itself must be resolvable
// from DI (Constitution Article VI.4).
builder.Services.AddScoped<IdempotencyFilter>();

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
