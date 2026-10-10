using System;
using System.Text.Json.Serialization;

namespace AssetHub.Application.Maintenance.Dtos;

public class AddPartRequest
{
    [JsonPropertyName("catalogItemId")]
    public Guid CatalogItemId { get; set; }
    
    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }
    
    [JsonPropertyName("unitCost")]
    public decimal UnitCost { get; set; }

    /// <summary>None | Internal | External</summary>
    [JsonPropertyName("sourceType")]
    public string SourceType { get; set; } = "None";

    [JsonPropertyName("warehouseId")]
    public Guid? WarehouseId { get; set; }

    [JsonPropertyName("externalSupplierName")]
    public string? ExternalSupplierName { get; set; }

    [JsonPropertyName("externalReference")]
    public string? ExternalReference { get; set; }
}
