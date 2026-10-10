using System;
using System.Collections.Generic;
using AssetHub.Domain.Catalogs;
using AssetHub.Domain.Finance;

namespace AssetHub.Domain.Staff;

public class Employee
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }

    // Idioma preferido para comunicaciones (codigo ISO, ej. "es", "en")
    public string PreferredLocale { get; set; } = "es";
    
    public Guid RoleCatalogItemId { get; set; }
    public CatalogItem? RoleCatalogItem { get; set; }
    
    // Almacena un array JSON de Guid correspondientes a los CatalogItems de las habilidades
    public string SkillsJson { get; set; } = "[]";
    
    public Guid? UserId { get; set; }
    
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }

    // Finance navigation
    public ICollection<AssetCustodyTransfer> CustodyTransfersFrom { get; set; } = new List<AssetCustodyTransfer>();
    public ICollection<AssetCustodyTransfer> CustodyTransfersTo { get; set; } = new List<AssetCustodyTransfer>();
}
