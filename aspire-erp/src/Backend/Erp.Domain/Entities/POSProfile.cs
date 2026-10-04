using System;
using Erp.Domain.Common;

namespace Erp.Domain.Entities;

public sealed class POSProfile : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    
    public string ProfileName { get; set; } = null!;
    
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    
    public Guid CashAccountId { get; set; }
    public Account? CashAccount { get; set; }
    
    public Guid CardClearingAccountId { get; set; }
    public Account? CardClearingAccount { get; set; }
    
    public Guid IncomeAccountId { get; set; }
    public Account? IncomeAccount { get; set; }
    
    public Guid? WriteOffAccountId { get; set; }
    public Account? WriteOffAccount { get; set; }
    
    public bool IsActive { get; set; } = true;
}
