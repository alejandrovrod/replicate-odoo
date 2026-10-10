using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AssetHub.Infrastructure.Persistence;

namespace AssetHub.Api.Controllers.Public;

[ApiController]
[Route("api/v1/public/plans")]
public class PlansController : ControllerBase
{
    private readonly PlatformDbContext _db;

    public PlansController(PlatformDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetPublicPlans()
    {
        var plans = await _db.Plans.AsNoTracking().ToListAsync();
        return Ok(new { items = plans });
    }
}
