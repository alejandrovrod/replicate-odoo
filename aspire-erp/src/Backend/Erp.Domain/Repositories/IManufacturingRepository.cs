using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for the manufacturing masters (Tasks 9.1-9.2): workstation and BOM
/// persistence. Minimal by design - Block B (Tasks 9.3/9.4) adds work-order and posting members.
/// Implemented by Erp.Infrastructure.Data.Repositories.ManufacturingRepository.
/// </summary>
public interface IManufacturingRepository
{
    /// <summary>Gets a workstation by its ID.</summary>
    Task<Workstation?> GetWorkstationByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets a BOM by its ID, including its items and operations.</summary>
    Task<BillOfMaterials?> GetBomByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Persists a new workstation.</summary>
    Task AddWorkstationAsync(Workstation workstation, CancellationToken cancellationToken = default);

    /// <summary>Persists a new BOM with its items and operations.</summary>
    Task AddBomAsync(BillOfMaterials bom, CancellationToken cancellationToken = default);
}
