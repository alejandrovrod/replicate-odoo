using System;

namespace AssetHub.Domain.Security;

public class Permission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Modulo funcional (para agrupar en UI), ej. "Activos", "Mantenimiento"
    public string Module { get; set; } = string.Empty;

    // Modulo de plan asociado (para R-ROLE-4): "assets", "maintenance", "billing", etc.
    // Vacio = permiso base disponible para todos los planes.
    public string PlanModule { get; set; } = string.Empty;
}
