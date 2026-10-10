using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AssetHub.Domain.Assets;

namespace AssetHub.Domain.Finance;

public class AssetDepreciationSchedule
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
    public int PeriodNumber { get; set; }

    [Required]
    public DateTime PeriodStartDate { get; set; }

    [Required]
    public DateTime PeriodEndDate { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,4)")]
    public decimal ProjectedDepreciationAmount { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,4)")]
    public decimal ProjectedAccumulatedDepreciation { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,4)")]
    public decimal ProjectedNetBookValue { get; set; }

    [Required]
    public bool IsPosted { get; set; } = false;

    public Guid? PostedEntryId { get; set; }

    [ForeignKey(nameof(PostedEntryId))]
    public AssetDepreciationEntry? PostedEntry { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}