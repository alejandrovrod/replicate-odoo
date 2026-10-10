using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Tenancy.Commands;

public record CheckSlugCommand(string Slug) : IRequest<CheckSlugResult>;

public record CheckSlugResult(bool Available);

public class CheckSlugCommandHandler : IRequestHandler<CheckSlugCommand, CheckSlugResult>
{
    private static readonly Regex SlugRegex = new(@"^[a-z0-9][a-z0-9-]{2,62}$", RegexOptions.Compiled);
    private static readonly string[] ReservedSlugs = { "www", "api", "app", "admin", "support" };
    
    private readonly IPlatformDbContext _platformDb;

    public CheckSlugCommandHandler(IPlatformDbContext platformDb)
    {
        _platformDb = platformDb;
    }

    public async Task<CheckSlugResult> Handle(CheckSlugCommand request, CancellationToken cancellationToken)
    {
        var slug = request.Slug?.ToLowerInvariant() ?? string.Empty;

        if (!SlugRegex.IsMatch(slug))
        {
            return new CheckSlugResult(false);
        }

        if (Array.Exists(ReservedSlugs, rs => rs == slug))
        {
            return new CheckSlugResult(false);
        }

        var exists = await _platformDb.Tenants.AnyAsync(t => t.Slug == slug, cancellationToken);
        
        return new CheckSlugResult(!exists);
    }
}
