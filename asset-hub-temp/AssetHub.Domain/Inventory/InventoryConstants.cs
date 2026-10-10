namespace AssetHub.Domain.Inventory;

public static class InventoryOperatingMode
{
    public const string External = "external";
    public const string Internal = "internal";
    public const string Hybrid = "hybrid";
}

public static class InventoryTransactionType
{
    public const string Receipt = "Receipt";
    public const string Issue = "Issue";
    public const string Adjustment = "Adjustment";
    public const string Reversal = "Reversal";
}

public static class InventoryTransactionState
{
    public const string Draft = "Draft";
    public const string Posted = "Posted";
    public const string Reversed = "Reversed";
}
