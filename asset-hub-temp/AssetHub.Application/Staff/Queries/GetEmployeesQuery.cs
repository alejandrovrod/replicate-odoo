using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

using AssetHub.Application.Staff.Helpers;

namespace AssetHub.Application.Staff.Queries;

public class GetEmployeesQuery : IRequest<GetEmployeesResult>
{
    public string? Search { get; set; }
    public bool? IsActive { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class GetEmployeesResult
{
    public List<EmployeeSummaryDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class EmployeeSummaryDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string PreferredLocale { get; set; } = "es";
    public Guid RoleCatalogItemId { get; set; }
    public string? RoleLabel { get; set; }
    public Guid? UserId { get; set; }
    public bool IsActive { get; set; }
}

public class GetEmployeesQueryHandler : IRequestHandler<GetEmployeesQuery, GetEmployeesResult>
{
    private readonly ITenantDbContext _db;
    private readonly ITenantResolver _tenantResolver;

    public GetEmployeesQueryHandler(ITenantDbContext db, ITenantResolver tenantResolver)
    {
        _db = db;
        _tenantResolver = tenantResolver;
    }

    public async Task<GetEmployeesResult> Handle(GetEmployeesQuery request, CancellationToken cancellationToken)
    {
        // Ensure role catalog exists for this tenant (lazy init on first page load)
        var tenantId = _tenantResolver.GetCurrentTenantId();
        await StaffCatalogDefaults.EnsureRoleCatalogAsync(_db, tenantId.Value, cancellationToken);

        var query = _db.Employees
            .AsNoTracking()
            .Include(e => e.RoleCatalogItem)
                .ThenInclude(ci => ci!.Translations)
            .Where(e => !e.IsDeleted)
            .AsQueryable();

        if (request.IsActive.HasValue)
        {
            query = query.Where(e => e.IsActive == request.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(e =>
                e.FirstName.ToLower().Contains(term) ||
                e.LastName.ToLower().Contains(term) ||
                e.Email.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(e => new EmployeeSummaryDto
            {
                Id = e.Id,
                FirstName = e.FirstName,
                LastName = e.LastName,
                Email = e.Email,
                PhoneNumber = e.PhoneNumber,
                PreferredLocale = e.PreferredLocale,
                RoleCatalogItemId = e.RoleCatalogItemId,
                RoleLabel = e.RoleCatalogItem != null
                    ? e.RoleCatalogItem.Translations
                        .Where(t => t.Locale == "es")
                        .Select(t => t.Label)
                        .FirstOrDefault()
                    : null,
                UserId = e.UserId,
                IsActive = e.IsActive
            })
            .ToListAsync(cancellationToken);

        return new GetEmployeesResult
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
