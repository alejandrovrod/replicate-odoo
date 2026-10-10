using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AssetHub.Domain.Assets;
using AssetHub.Domain.Maintenance;

namespace AssetHub.Domain.Finance;

public class AssetRepairCapitalization
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid MaintenanceOrderId { get; set; }

    [ForeignKey(nameof(MaintenanceOrderId))]
    public MaintenanceOrder? MaintenanceOrder { get; set; }

    [Required]
    public Guid AssetId { get; set; }

    [ForeignKey(nameof(AssetId))]
    public Asset? Asset { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,4)")]
    public decimal CapitalizedAmount { get; set; }

    public int? NewUsefulLifeMonths { get; set; }

    [Required]
    public DateTime EffectiveDate { get; set; }

    [Required]
    public Guid ApprovedBy { get; set; }

    [Required]
    public DateTime ApprovedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}