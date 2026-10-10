using System;
using NetTopologySuite.Geometries;

namespace AssetHub.Domain.Assets;

public enum AssetAttributeValueType
{
    Text,
    Number,
    Date,
    Bool,
    Catalog,
    Geo,
    Json
}

public class AssetAttributeValue
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; } // Agregado para índice
    public Guid AssetId { get; set; }
    public Asset? Asset { get; set; }

    public string AttributeKey { get; set; } = string.Empty;
    public AssetAttributeValueType ValueType { get; set; }

    public string? ValueText { get; set; }
    public decimal? ValueNumber { get; set; }
    public DateTime? ValueDate { get; set; }
    public bool? ValueBool { get; set; }
    public Guid? ValueCatalogItemId { get; set; }
    public Geometry? ValueGeo { get; set; }
    public string? ValueJson { get; set; }
}
