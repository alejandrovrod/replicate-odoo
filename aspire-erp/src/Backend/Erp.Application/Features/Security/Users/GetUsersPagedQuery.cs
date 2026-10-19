using Erp.Application.Common;
using Erp.Domain.Common;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Security.Users;

/// <summary>
/// Paged, text-filtered read of the current tenant's users (spec D2 USER-G1).
/// Pagination follows the Standard Pagination Pattern; <c>Search</c> matches email or full name.
/// </summary>
public sealed record GetUsersPagedQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? Search = null) : IQuery<PagedResult<UserAdminDto>>;

public sealed class GetUsersPagedQueryHandler
    : IQueryHandler<GetUsersPagedQuery, PagedResult<UserAdminDto>>
{
    private readonly IUserAdminRepository _users;

    public GetUsersPagedQueryHandler(IUserAdminRepository users)
    {
        _users = users;
    }

    public async Task<PagedResult<UserAdminDto>> HandleAsync(
        GetUsersPagedQuery query,
        CancellationToken cancellationToken = default)
    {
        var paging = new PagedRequest(query.PageNumber, query.PageSize);
        var page = await _users.GetPagedAsync(paging, query.Search, cancellationToken);

        return page.Map(page.Items.Select(UserAdminDto.From).ToList());
    }
}
