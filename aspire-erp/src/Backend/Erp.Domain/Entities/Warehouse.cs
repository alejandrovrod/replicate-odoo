using Erp.Domain.Common;

namespace Erp.Domain.Entities;

public class Warehouse : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string WarehouseCode { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;

    public Guid? ParentWarehouseId { get; set; }
    public Warehouse? Parent { get; set; }
    public ICollection<Warehouse> Children { get; set; } = new List<Warehouse>();

    public bool IsGroup { get; set; }
    public string Type { get; set; } = "Physical";

    public Guid? AccountId { get; set; }
    public Account? Account { get; set; }

    public bool IsActive { get; set; } = true;
}
