using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AssetHub.Domain.Assets;

namespace AssetHub.Domain.Finance;

public class AssetFinanceBook
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
    [Column(TypeName = "decimal(18,4)")]
    public decimal AcquisitionCost { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,4)")]
    public decimal ResidualValue { get; set; }

    [Required]
    public int UsefulLifeMonths { get; set; }

    [Required]
    public DepreciationMethod DepreciationMethod { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? DepreciationRatePct { get; set; }

    [Required]
    public int FrequencyMonths { get; set; } = 1;

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = "MXN";

    [Required]
    public bool IsActive { get; set; } = true;

    [Timestamp]
    public byte[]? RowVersion { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public decimal DepreciableBase => AcquisitionCost - ResidualValue;

    public decimal MonthlyStraightLineAmount =>
        DepreciationMethod == DepreciationMethod.StraightLine && UsefulLifeMonths > 0
            ? Math.Round(DepreciableBase / UsefulLifeMonths, 4)
            : 0;

    public double DoubleDecliningRate =>
        DepreciationMethod == DepreciationMethod.DoubleDeclining && UsefulLifeMonths > 0
            ? 2.0 / UsefulLifeMonths
            : 0;
}