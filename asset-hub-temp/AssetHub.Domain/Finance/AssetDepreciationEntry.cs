using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AssetHub.Domain.Assets;

namespace AssetHub.Domain.Finance;

public class AssetDepreciationEntry
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
    public Guid ScheduleId { get; set; }

    [ForeignKey(nameof(ScheduleId))]
    public AssetDepreciationSchedule? Schedule { get; set; }

    [Required]
    public int PeriodNumber { get; set; }

    [Required]
    public DateTime AccountingDate { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,4)")]
    public decimal DepreciationAmount { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,4)")]
    public decimal AccumulatedDepreciation { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,4)")]
    public decimal NetBookValue { get; set; }

    [Required]
    [MaxLength(64)]
    public string IdempotencyKey { get; set; } = string.Empty;

    [Required]
    public Guid PostedBy { get; set; }

    [Required]
    public DateTime PostedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}