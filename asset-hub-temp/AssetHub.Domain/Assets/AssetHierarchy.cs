using System;

namespace AssetHub.Domain.Assets;

public class AssetHierarchy
{
    public Guid AncestorId { get; set; }
    public Asset? Ancestor { get; set; }

    public Guid DescendantId { get; set; }
    public Asset? Descendant { get; set; }

    public int Depth { get; set; }
}
