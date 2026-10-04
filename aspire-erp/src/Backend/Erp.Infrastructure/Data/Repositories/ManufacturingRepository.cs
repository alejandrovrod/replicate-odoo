using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IManufacturingRepository"/>: workstation and BOM
/// persistence. No manual <c>.Where(e =&gt; e.TenantId == ...)</c> on LINQ queries
/// (Constitution II.3 - the global query filter does it).
/// </summary>
public sealed class ManufacturingRepository : IManufacturingRepository
{
    private readonly AppDbContext _dbContext;

    public ManufacturingRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Workstation?> GetWorkstationByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _dbContext.Workstations.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<BillOfMaterials?> GetBomByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _dbContext.BillsOfMaterials
            .Include(e => e.Items)
            .Include(e => e.Operations)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task AddWorkstationAsync(Workstation workstation, CancellationToken cancellationToken = default)
    {
        await _dbContext.Workstations.AddAsync(workstation, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddBomAsync(BillOfMaterials bom, CancellationToken cancellationToken = default)
    {
        await _dbContext.BillsOfMaterials.AddAsync(bom, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
