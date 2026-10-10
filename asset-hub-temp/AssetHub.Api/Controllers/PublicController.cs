using AssetHub.Application.Interfaces;
using AssetHub.Domain.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/v1/public")]
[EnableRateLimiting("PublicApi")]
public class PublicController : ControllerBase
{
    private readonly IPlatformDbContext _platformDb;
    private readonly IMemoryCache _cache;

    public PublicController(IPlatformDbContext platformDb, IMemoryCache cache)
    {
        _platformDb = platformDb;
        _cache = cache;
    }

    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans()
    {
        const string cacheKey = "public_plans";
        
        if (!_cache.TryGetValue(cacheKey, out List<Plan>? publicPlans))
        {
            publicPlans = await _platformDb.Plans
                .Where(p => p.IsPublic && !p.IsDeleted)
                .OrderBy(p => p.PriceMonthly)
                .ToListAsync();

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));

            _cache.Set(cacheKey, publicPlans, cacheEntryOptions);
        }

        return Ok(publicPlans);
    }
}
