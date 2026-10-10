using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AssetHub.Domain.Assets;

namespace AssetHub.Domain.Finance;

public class AssetCustodyTransfer
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid AssetId { get; set; }

    [ForeignKey(nameof(AssetId))]
    public Asset? Asset { get; set; }

    public Guid? FromEmployeeId { get; set; }

    [ForeignKey(nameof(FromEmployeeId))]
    public AssetHub.Domain.Staff.Employee? FromEmployee { get; set; }

    [Required]
    public Guid ToEmployeeId { get; set; }

    [ForeignKey(nameof(ToEmployeeId))]
    public AssetHub.Domain.Staff.Employee? ToEmployee { get; set; }

    public Guid? FromDepartmentId { get; set; }

    public Guid? ToDepartmentId { get; set; }

    [Required]
    public DateTime TransferDate { get; set; }

    [Required]
    public CustodyTransferType TransferType { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? DocumentUrl { get; set; }

    public Guid? SignedByFrom { get; set; }

    public Guid? SignedByTo { get; set; }

    [Required]
    public Guid CreatedBy { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}