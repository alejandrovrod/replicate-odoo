using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AssetHub.Domain.Assets;

namespace AssetHub.Domain.Finance;

public class AssetValueAdjustment
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid AssetId { get; set; }

    [ForeignKey(nameof(AssetId))]
    public Asset? Asset { get; set; }

    [Required]
    public Guid FinanceBookId { get; set; }

    [ForeignKey(nameof(FinanceBookId))]
    public AssetFinanceBook? FinanceBook { get; set; }

    [Required]
    public ValueAdjustmentType AdjustmentType { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,4)")]
    public decimal PreviousNetBookValue { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,4)")]
    public decimal AdjustmentAmount { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,4)")]
    public decimal NewNetBookValue { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Reason { get; set; } = string.Empty;

    [Required]
    public DateTime EffectiveDate { get; set; }

    [Required]
    public Guid ApprovedBy { get; set; }

    [Required]
    public DateTime ApprovedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsPosted => true; // Always posted/immutable upon creation
}