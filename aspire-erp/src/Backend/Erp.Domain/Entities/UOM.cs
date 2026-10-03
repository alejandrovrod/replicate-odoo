using Erp.Domain.Common;

namespace Erp.Domain.Entities;

public class UOM : ITenantEntity
{
    public Guid Id { get; set; }
    
    public Guid TenantId { get; set; }
    
    public Guid CompanyId { get; set; }

    public string UomName { get; set; } = string.Empty;

    public string Symbol { get; set; } = string.Empty;

    public bool MustBeWholeNumber { get; set; }

    public bool IsActive { get; set; } = true;
}
