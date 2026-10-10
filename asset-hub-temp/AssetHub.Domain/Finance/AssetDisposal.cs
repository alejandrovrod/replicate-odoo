using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AssetHub.Domain.Assets;

namespace AssetHub.Domain.Finance;

public class AssetDisposal
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
    public DisposalType DisposalType { get; set; }

    [Required]
    public DateTime DisposalDate { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,4)")]
    public decimal NetBookValueAtDisposal { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,4)")]
    public decimal ProceedsAmount { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,4)")]
    public decimal GainLossAmount { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? DocumentReference { get; set; }

    [Required]
    public Guid ApprovedBy { get; set; }

    [Required]
    public DateTime ApprovedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}